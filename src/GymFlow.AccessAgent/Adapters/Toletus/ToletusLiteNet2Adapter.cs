using System.Globalization;
using System.Net;
using GymFlow.AccessAgent.Abstractions;
using GymFlow.AccessAgent.Api;
using GymFlow.AccessAgent.Offline;
using GymFlow.AccessAgent.Services;
using Microsoft.Extensions.Options;
using Toletus.LiteNet2;
using Toletus.LiteNet2.Base;
using Toletus.LiteNet2.Command;
using Toletus.LiteNet2.Command.Enums;
using Toletus.LiteNet2.Enums;

namespace GymFlow.AccessAgent.Adapters.Toletus;

public sealed class ToletusLiteNet2Adapter :
    IAccessDeviceAdapter,
    IDisposable
{
    public const string Provider =
        "toletus-litenet2";

    private readonly ToletusLiteNet2Options _options;

    private readonly ILogger<ToletusLiteNet2Adapter>
        _logger;

    private readonly IAccessReleaseControl
        _releaseControl;

    private readonly PhysicalPassageConfirmationCoordinator
        _passageConfirmation;

    private readonly SemaphoreSlim _processingLock =
        new(1, 1);

    private readonly object _statusLock =
        new();

    private LiteNet2Board? _board;

    private AgentDeviceConfiguration?
        _activeConfiguration;

    private DateTime? _lastCredentialReadAt;

    private AccessCredentialType?
        _lastCredentialType;

    private volatile bool _connected;

    private Func<
        DeviceAccessAttempt,
        CancellationToken,
        Task<AccessDeviceDecision>>? _handleAttemptAsync;

    private CancellationToken _stoppingToken;

    public ToletusLiteNet2Adapter(
        IOptions<ToletusLiteNet2Options> options,
        ILogger<ToletusLiteNet2Adapter> logger,
        IAccessReleaseControl releaseControl,
        IPhysicalPassageOfflineStore passageStore,
        TimeProvider clock)
    {
        _options = options.Value;
        _logger = logger;
        _releaseControl = releaseControl;
        _passageConfirmation = new PhysicalPassageConfirmationCoordinator(
            passageStore,
            clock,
            _options.EnablePhysicalPassageConfirmation);
    }

    public string ProviderKey =>
        Provider;

    public AccessDeviceRuntimeStatus
        GetRuntimeStatus()
    {
        AgentDeviceConfiguration?
            configuration;

        DateTime?
            lastCredentialReadAt;

        AccessCredentialType?
            lastCredentialType;

        lock (_statusLock)
        {
            configuration =
                _activeConfiguration;

            lastCredentialReadAt =
                _lastCredentialReadAt;

            lastCredentialType =
                _lastCredentialType;
        }

        var deviceKey =
            string.IsNullOrWhiteSpace(
                configuration?.DeviceKey)
                ? "primary"
                : configuration.DeviceKey.Trim();

        var enabled =
            configuration?.Enabled == true;

        var endpoint =
            configuration is null ||
            string.IsNullOrWhiteSpace(
                configuration.Host)
                ? null
                : $"{configuration.Host.Trim()}:{configuration.Port}";

        return new AccessDeviceRuntimeStatus(
            deviceKey,
            Provider,
            enabled,
            enabled &&
                _connected,
            endpoint,
            lastCredentialReadAt,
            lastCredentialType);
    }

    public async Task StartAsync(
        Func<
            DeviceAccessAttempt,
            CancellationToken,
            Task<AccessDeviceDecision>>
            handleAttemptAsync,
        CancellationToken cancellationToken)
    {
        _connected = false;

        _handleAttemptAsync =
            handleAttemptAsync;

        _stoppingToken =
            cancellationToken;

        var reconnectDelay =
            TimeSpan.FromSeconds(
                Math.Max(
                    1,
                    _options.ReconnectDelaySeconds));

        try
        {
            while (!cancellationToken
                       .IsCancellationRequested)
            {
                AgentDeviceConfiguration?
                    configuration;

                try
                {
                    configuration =
                        await GetDesiredConfigurationAsync(
                            cancellationToken);
                }
                catch (Exception ex)
                    when (ex is not OperationCanceledException)
                {
                    _connected = false;

                    _logger.LogWarning(
                        ex,
                        "Não foi possível ler a configuração local da catraca.");

                    await Task.Delay(
                        reconnectDelay,
                        cancellationToken);

                    continue;
                }

                SetActiveConfiguration(
                    configuration);

                if (configuration is null ||
                    !configuration.Enabled)
                {
                    _connected = false;

                    await Task.Delay(
                        TimeSpan.FromSeconds(1),
                        cancellationToken);

                    continue;
                }

                if (!IPAddress.TryParse(
                        configuration.Host,
                        out var ipAddress))
                {
                    _connected = false;

                    _logger.LogError(
                        "O endereço IP configurado para a LiteNet2 é inválido. O Agent continuará em execução sem conexão com a catraca.");

                    await Task.Delay(
                        reconnectDelay,
                        cancellationToken);

                    continue;
                }

                var configurationChanged =
                    false;

                LiteNet2Board? board =
                    null;

                try
                {
                    board =
                        new LiteNet2Board(
                            ipAddress,
                            "AVELRI",
                            0)
                        {
                            ConnectPort =
                                configuration.Port
                        };

                    _board =
                        board;

                    board.OnIdentification +=
                        BoardOnIdentification;

                    if (_options.EnablePhysicalPassageConfirmation)
                    {
                        board.OnGyre += BoardOnGyre;
                        board.OnResponse += BoardOnResponse;
                    }

                    _logger.LogInformation(
                        "Conectando à Toletus LiteNet2 em {Host}:{Port}.",
                        configuration.Host,
                        configuration.Port);

                    board.Connect();

                    if (!board.Connected)
                    {
                        throw new InvalidOperationException(
                            "Não foi possível estabelecer conexão com a LiteNet2.");
                    }

                    _connected = true;

                    _logger.LogInformation(
                        "Conexão estabelecida com a Toletus LiteNet2 em {Host}:{Port}.",
                        configuration.Host,
                        configuration.Port);

                    while (
                        board.Connected &&
                        !cancellationToken
                            .IsCancellationRequested)
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(1),
                            cancellationToken);

                        var latestConfiguration =
                            await GetDesiredConfigurationAsync(
                                cancellationToken);

                        SetActiveConfiguration(
                            latestConfiguration);

                        if (!ConfigurationMatches(
                                configuration,
                                latestConfiguration))
                        {
                            configurationChanged =
                                true;

                            _logger.LogInformation(
                                "A configuração da catraca foi alterada. A conexão será recriada.");

                            break;
                        }
                    }

                    if (!board.Connected &&
                        !cancellationToken
                            .IsCancellationRequested)
                    {
                        _logger.LogWarning(
                            "Conexão com a Toletus LiteNet2 foi perdida.");
                    }
                }
                catch (OperationCanceledException)
                    when (cancellationToken
                        .IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Falha na conexão com a Toletus LiteNet2. Nova tentativa em {ReconnectDelaySeconds}s.",
                        reconnectDelay.TotalSeconds);
                }
                finally
                {
                    _connected = false;

                    if (board is not null)
                    {
                        board.OnIdentification -=
                            BoardOnIdentification;

                        board.OnGyre -= BoardOnGyre;
                        board.OnResponse -= BoardOnResponse;
                        _passageConfirmation.ClearForDevice(board);

                        if (board.Connected)
                        {
                            board.Close();
                        }
                    }

                    _board = null;
                }

                if (!cancellationToken
                         .IsCancellationRequested &&
                    !configurationChanged)
                {
                    await Task.Delay(
                        reconnectDelay,
                        cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
        }
        finally
        {
            _connected = false;
        }
    }

    public async Task ApplyDecisionAsync(
        DeviceAccessAttempt attempt,
        AccessDeviceDecision decision,
        CancellationToken cancellationToken)
    {
        if (!decision.Allowed)
        {
            _logger.LogInformation(
                "Acesso negado pela Avelri. RequestId: {RequestId}, Reason: {Reason}.",
                attempt.RequestId,
                decision.Reason);

            return;
        }

        if (!await _releaseControl
                .IsReleaseEnabledAsync(
                    cancellationToken))
        {
            _logger.LogInformation(
                "Acesso autorizado pela Avelri, mas o acionamento físico está desativado. RequestId: {RequestId}.",
                attempt.RequestId);

            return;
        }

        var board =
            _board ??
            throw new InvalidOperationException(
                "A LiteNet2 não está conectada.");

        if (!board.Connected)
        {
            _connected = false;

            throw new InvalidOperationException(
                "A conexão com a LiteNet2 foi perdida.");
        }

        if (_options.EnablePhysicalPassageConfirmation)
        {
            var deviceKey = GetConfiguredDeviceKey();
            if (!_passageConfirmation.TryArm(
                    board,
                    deviceKey,
                    attempt.RequestId,
                    DateTime.UtcNow))
            {
                _logger.LogWarning(
                    "Acesso autorizado, mas a liberação foi retida enquanto outra passagem aguarda confirmação física. RequestId: {RequestId}, DeviceKey: {DeviceKey}.",
                    attempt.RequestId,
                    deviceKey);
                return;
            }
        }

        try
        {
            board.ReleaseEntryAndExit(
                "ACESSO LIBERADO");
        }
        catch
        {
            _passageConfirmation.Cancel(attempt.RequestId);
            throw;
        }
    }

    private async Task<AgentDeviceConfiguration?>
        GetDesiredConfigurationAsync(
            CancellationToken cancellationToken)
    {
        var configuration =
            await _releaseControl
                .GetConfigurationAsync(
                    cancellationToken);

        return configuration
            .DesiredDevices
            .FirstOrDefault(
                device =>
                    string.Equals(
                        device.ProviderKey,
                        Provider,
                        StringComparison.OrdinalIgnoreCase));
    }

    private void SetActiveConfiguration(
        AgentDeviceConfiguration? configuration)
    {
        lock (_statusLock)
        {
            _activeConfiguration =
                configuration;
        }
    }

    private static bool ConfigurationMatches(
        AgentDeviceConfiguration current,
        AgentDeviceConfiguration? latest)
    {
        if (latest is null)
            return false;

        return
            string.Equals(
                current.DeviceKey,
                latest.DeviceKey,
                StringComparison.Ordinal) &&
            string.Equals(
                current.ProviderKey,
                latest.ProviderKey,
                StringComparison.OrdinalIgnoreCase) &&
            current.Enabled ==
                latest.Enabled &&
            string.Equals(
                current.Host,
                latest.Host,
                StringComparison.OrdinalIgnoreCase) &&
            current.Port ==
                latest.Port;
    }

    private void BoardOnIdentification(
        LiteNet2BoardBase board,
        Identification identification)
    {
        if (_handleAttemptAsync is null)
            return;

        if (!TryCreateAttempt(
                identification,
                out var attempt) ||
            attempt is null)
        {
            return;
        }

        RecordCredentialRead(
            attempt);

        _ = ProcessAttemptAsync(
            attempt,
            _stoppingToken);
    }

    private void BoardOnGyre(
        LiteNet2Board board,
        Direction direction)
    {
        if (direction is not (Direction.Entry or Direction.Exit) ||
            !ReferenceEquals(_board, board))
            return;

        var deviceKey = GetConfiguredDeviceKey();
        _ = RecordPhysicalPassageAsync(board, deviceKey);
    }

    private async Task RecordPhysicalPassageAsync(
        LiteNet2Board board,
        string deviceKey)
    {
        try
        {
            var occurredAt = DateTime.UtcNow;
            if (!await _passageConfirmation.ConfirmAsync(
                    board,
                    deviceKey,
                    occurredAt,
                    _stoppingToken))
                return;

            _logger.LogInformation(
                "Passagem física confirmada pelo callback OnGyre da LiteNet2. DeviceKey: {DeviceKey}, OccurredAt: {OccurredAt}.",
                deviceKey,
                occurredAt);
        }
        catch (OperationCanceledException)
            when (_stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao persistir confirmação de passagem física. DeviceKey: {DeviceKey}.",
                deviceKey);
        }
    }

    private void BoardOnResponse(
        LiteNet2Board board,
        LiteNet2Response response)
    {
        if (ReferenceEquals(_board, board) &&
            response.Command == LiteNet2Commands.GyreTimeout)
            _passageConfirmation.HandleReleaseTimeout(board);
    }

    private string GetConfiguredDeviceKey()
    {
        lock (_statusLock)
        {
            return string.IsNullOrWhiteSpace(_activeConfiguration?.DeviceKey)
                ? "primary"
                : _activeConfiguration.DeviceKey.Trim();
        }
    }

    private void RecordCredentialRead(
        DeviceAccessAttempt attempt)
    {
        lock (_statusLock)
        {
            _lastCredentialReadAt =
                attempt.OccurredAt
                    .ToUniversalTime();

            _lastCredentialType =
                attempt.CredentialType;
        }
    }

    private async Task ProcessAttemptAsync(
        DeviceAccessAttempt attempt,
        CancellationToken cancellationToken)
    {
        await _processingLock.WaitAsync(
            cancellationToken);

        try
        {
            if (_handleAttemptAsync is null)
                return;

            await _handleAttemptAsync(
                attempt,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken
                .IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao processar tentativa de acesso da LiteNet2. RequestId: {RequestId}.",
                attempt.RequestId);
        }
        finally
        {
            _processingLock.Release();
        }
    }

    private static bool TryCreateAttempt(
        Identification identification,
        out DeviceAccessAttempt? attempt)
    {
        attempt = null;

        var credentialType =
            identification.Device switch
            {
                IdentificationDevice.Rfid =>
                    AccessCredentialType.Card,

                IdentificationDevice.BarCode =>
                    AccessCredentialType.Card,

                IdentificationDevice.Keyboard =>
                    AccessCredentialType.PinExternalId,

                IdentificationDevice.EmbeddedFingerprint
                    when identification.Data > 0 =>
                    AccessCredentialType
                        .BiometricExternalId,

                _ => (AccessCredentialType?)null
            };

        if (credentialType is null)
            return false;

        if (credentialType == AccessCredentialType.PinExternalId &&
            identification.Data > 9999)
            return false;

        var externalIdentifier =
            credentialType == AccessCredentialType.PinExternalId
                ? identification.Data.ToString("D4", CultureInfo.InvariantCulture)
                : Convert.ToString(identification.Data, CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(
                externalIdentifier))
        {
            return false;
        }

        attempt =
            new DeviceAccessAttempt(
                Guid.NewGuid(),
                externalIdentifier,
                credentialType.Value,
                DateTime.UtcNow);

        return true;
    }

    public void Dispose()
    {
        _connected = false;
        _passageConfirmation.Clear();

        if (_board?.Connected == true)
        {
            _board.Close();
        }

        _processingLock.Dispose();
    }
}
