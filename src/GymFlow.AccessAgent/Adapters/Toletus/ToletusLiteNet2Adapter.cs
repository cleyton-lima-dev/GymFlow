using GymFlow.AccessAgent.Abstractions;
using GymFlow.AccessAgent.Services;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
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
    private readonly ILogger<ToletusLiteNet2Adapter> _logger;
    private readonly IAccessReleaseControl _releaseControl;
    private readonly SemaphoreSlim _processingLock = new(1, 1);

    private LiteNet2Board? _board;

    private Func<
        DeviceAccessAttempt,
        CancellationToken,
        Task<AccessDeviceDecision>>? _handleAttemptAsync;

    private CancellationToken _stoppingToken;

    public ToletusLiteNet2Adapter(
        IOptions<ToletusLiteNet2Options> options,
        ILogger<ToletusLiteNet2Adapter> logger,
        IAccessReleaseControl releaseControl)
    {
        _options = options.Value;
        _logger = logger;
        _releaseControl = releaseControl;
    }

    public string ProviderKey => Provider;

    public async Task StartAsync(
        Func<
            DeviceAccessAttempt,
            CancellationToken,
            Task<AccessDeviceDecision>> handleAttemptAsync,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Adapter Toletus LiteNet2 está desabilitado.");

            await Task.Delay(
                Timeout.Infinite,
                cancellationToken);

            return;
        }

        if (!IPAddress.TryParse(
                _options.Host,
                out var ipAddress))
        {
            throw new InvalidOperationException(
                "O endereço IP configurado para a LiteNet2 é inválido.");
        }

        _handleAttemptAsync = handleAttemptAsync;
        _stoppingToken = cancellationToken;

        var reconnectDelay =
            TimeSpan.FromSeconds(
                Math.Max(
                    1,
                    _options.ReconnectDelaySeconds));

        while (!cancellationToken.IsCancellationRequested)
        {
            LiteNet2Board? board = null;

            try
            {
                board =
                    new LiteNet2Board(
                        ipAddress,
                        "AVELRI",
                        0)
                    {
                        ConnectPort = _options.Port
                    };

                _board = board;

                board.OnIdentification +=
                    BoardOnIdentification;

                _logger.LogInformation(
                    "Conectando à Toletus LiteNet2 em {Host}:{Port}.",
                    _options.Host,
                    _options.Port);

                board.Connect();

                if (!board.Connected)
                {
                    throw new InvalidOperationException(
                        "Não foi possível estabelecer conexão com a LiteNet2.");
                }

                _logger.LogInformation(
                    "Conexão estabelecida com a Toletus LiteNet2 em {Host}:{Port}.",
                    _options.Host,
                    _options.Port);

                while (
                    board.Connected &&
                    !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(1),
                        cancellationToken);
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        "Conexão com a Toletus LiteNet2 foi perdida.");
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
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
                if (board is not null)
                {
                    board.OnIdentification -=
                        BoardOnIdentification;

                    if (board.Connected)
                    {
                        board.Close();
                    }
                }

                _board = null;
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(
                    reconnectDelay,
                    cancellationToken);
            }
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

        if (!await _releaseControl.IsReleaseEnabledAsync(
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
            throw new InvalidOperationException(
                "A conexão com a LiteNet2 foi perdida.");
        }

        board.ReleaseEntryAndExit(
            "ACESSO LIBERADO");
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

        _ = ProcessAttemptAsync(
            attempt,
            _stoppingToken);
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
            when (cancellationToken.IsCancellationRequested)
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
                    AccessCredentialType.BiometricExternalId,

                _ => (AccessCredentialType?)null
            };

        if (credentialType is null)
            return false;

        var externalIdentifier =
            Convert.ToString(
                identification.Data,
                CultureInfo.InvariantCulture);

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
        if (_board?.Connected == true)
        {
            _board.Close();
        }

        _processingLock.Dispose();
    }
}
