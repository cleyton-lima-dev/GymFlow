using GymFlow.AccessAgent.Api;

namespace GymFlow.AccessAgent.Security;

public static class AgentPairingBootstrap
{
    public static bool IsPairingRequested(
        IEnumerable<string> args)
    {
        return args.Any(
            argument =>
                string.Equals(
                    argument,
                    "--pair",
                    StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryValidateApiBaseUrl(
        string? value,
        out string? normalizedBaseUrl,
        out string? error)
    {
        normalizedBaseUrl = null;
        error = null;

        var apiBaseUrl =
            value?.Trim();

        if (string.IsNullOrWhiteSpace(apiBaseUrl) ||
            !Uri.TryCreate(
                apiBaseUrl,
                UriKind.Absolute,
                out var apiUri))
        {
            error =
                "A URL da API não está configurada corretamente.";

            return false;
        }

        var isHttps =
            string.Equals(
                apiUri.Scheme,
                Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase);

        var isLocalHttp =
            string.Equals(
                apiUri.Scheme,
                Uri.UriSchemeHttp,
                StringComparison.OrdinalIgnoreCase) &&
            apiUri.IsLoopback;

        if (!isHttps && !isLocalHttp)
        {
            error =
                "HTTPS é obrigatório fora do ambiente local.";

            return false;
        }

        normalizedBaseUrl =
            apiBaseUrl.TrimEnd('/');

        return true;
    }

    public static bool TryCreateCredentials(
        PairAccessAgentResult? result,
        string apiBaseUrl,
        out AgentCredentials? credentials,
        out string? error)
    {
        credentials = null;
        error = null;

        if (result is null ||
            result.AgentId == Guid.Empty ||
            result.GymId == Guid.Empty ||
            string.IsNullOrWhiteSpace(
                result.Secret))
        {
            error =
                "A API retornou dados de pareamento inválidos.";

            return false;
        }

        if (!TryValidateApiBaseUrl(
                apiBaseUrl,
                out var normalizedBaseUrl,
                out error))
        {
            return false;
        }

        credentials =
            new AgentCredentials(
                result.AgentId,
                result.GymId,
                result.Secret,
                normalizedBaseUrl!);

        return true;
    }

    public static async Task<int> RunAsync(
        IAccessAgentPairingClient pairingClient,
        IAgentCredentialStore credentialStore,
        ILocalAgentStateProbe localStateProbe,
        string? apiBaseUrl,
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine(
            "Avelri Access Agent - Pareamento");
        Console.WriteLine();

        if (!TryValidateApiBaseUrl(
                apiBaseUrl,
                out var normalizedBaseUrl,
                out var configurationError))
        {
            Console.WriteLine(
                $"ERRO: {configurationError}");

            return 1;
        }

        var machineName =
            Environment.MachineName;

        Console.WriteLine(
            $"Máquina: {machineName}");

        Console.Write(
            "Código de pareamento: ");

        var pairingCode =
            Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(
                pairingCode))
        {
            Console.WriteLine();
            Console.WriteLine(
                "ERRO: O código de pareamento é obrigatório.");

            return 1;
        }

        var outcome = await PairWithCodeAsync(
            pairingClient,
            credentialStore,
            localStateProbe,
            normalizedBaseUrl!,
            pairingCode,
            machineName,
            cancellationToken);

        if (!outcome.Success)
        {
            Console.WriteLine();
            Console.WriteLine($"ERRO: {outcome.Error}");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine(
            "Pareamento concluído com sucesso.");
        Console.WriteLine(
            "A credencial do Agent foi armazenada com segurança via DPAPI.");

        return 0;
    }

    public static async Task<PairingOutcome> PairWithCodeAsync(
        IAccessAgentPairingClient pairingClient,
        IAgentCredentialStore credentialStore,
        ILocalAgentStateProbe localStateProbe,
        string apiBaseUrl,
        string pairingCode,
        string machineName,
        CancellationToken cancellationToken)
    {
        IDisposable pairingLock;
        try
        {
            pairingLock = localStateProbe.AcquirePairingLock();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return PairingOutcome.Fail(
                "Não foi possível obter acesso exclusivo aos dados locais. O pareamento foi bloqueado.");
        }

        using var heldPairingLock = pairingLock;

        AgentCredentials? previous;

        try
        {
            previous = await credentialStore.LoadAsync(cancellationToken);

            if (previous is null)
            {
                if (localStateProbe.HasPotentialLocalState())
                    return PairingOutcome.Fail(
                        "Há dados locais sem identidade verificável. O pareamento foi bloqueado para proteger o tenant anterior.");
            }
            else if (!HasValidIdentity(previous))
            {
                return PairingOutcome.Fail(
                    "A identidade local anterior é inválida. O pareamento foi bloqueado.");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return PairingOutcome.Fail(
                "Não foi possível verificar a identidade local anterior. O pareamento foi bloqueado.");
        }

        PairAccessAgentResult? result;
        try
        {
            result = await pairingClient.PairAsync(
                apiBaseUrl,
                pairingCode,
                machineName,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return PairingOutcome.Fail("Não foi possível acessar a API.");
        }

        if (result is null)
            return PairingOutcome.Fail("Código inválido, expirado ou já utilizado.");

        if (!TryCreateCredentials(
                result,
                apiBaseUrl,
                out var credentials,
                out var credentialsError))
        {
            return PairingOutcome.Fail(credentialsError!);
        }

        if (previous is not null && previous.GymId != credentials!.GymId)
        {
            return PairingOutcome.Fail(
                "O código pertence a outra academia. A credencial e os dados locais anteriores foram preservados.");
        }

        try
        {
            // Reject an identity changed while the pairing request was in flight.
            var current = await credentialStore.LoadAsync(cancellationToken);
            if (current != previous ||
                (current is null && localStateProbe.HasPotentialLocalState()))
            {
                return PairingOutcome.Fail(
                    "A identidade local mudou durante o pareamento. Nenhuma credencial foi substituída.");
            }

            await credentialStore.SaveAsync(
                credentials!,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return PairingOutcome.Fail(
                "Não foi possível gravar a credencial com segurança. O serviço deve permanecer parado.");
        }

        return PairingOutcome.Ok();
    }

    private static bool HasValidIdentity(AgentCredentials credentials)
    {
        return credentials.AgentId != Guid.Empty &&
               credentials.GymId != Guid.Empty &&
               !string.IsNullOrWhiteSpace(credentials.Secret) &&
               TryValidateApiBaseUrl(
                   credentials.ApiBaseUrl,
                   out _,
                   out _);
    }
}

public sealed record PairingOutcome(bool Success, string? Error)
{
    public static PairingOutcome Ok() => new(true, null);
    public static PairingOutcome Fail(string error) => new(false, error);
}
