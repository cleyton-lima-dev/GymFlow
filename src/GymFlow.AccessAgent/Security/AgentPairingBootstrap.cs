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

        PairAccessAgentResult? result;

        try
        {
            result =
                await pairingClient.PairAsync(
                    normalizedBaseUrl!,
                    pairingCode,
                    machineName,
                    cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"ERRO: Não foi possível acessar a API: {ex.Message}");

            return 1;
        }

        if (result is null)
        {
            Console.WriteLine();
            Console.WriteLine(
                "ERRO: Código inválido, expirado ou já utilizado.");

            return 1;
        }

        if (!TryCreateCredentials(
                result,
                normalizedBaseUrl!,
                out var credentials,
                out var credentialsError))
        {
            Console.WriteLine();
            Console.WriteLine(
                $"ERRO: {credentialsError}");

            return 1;
        }

        await credentialStore.SaveAsync(
            credentials!,
            cancellationToken);

        Console.WriteLine();
        Console.WriteLine(
            "Pareamento concluído com sucesso.");
        Console.WriteLine(
            "A credencial do Agent foi armazenada com segurança via DPAPI.");

        return 0;
    }
}
