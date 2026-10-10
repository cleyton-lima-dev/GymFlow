using System.Text;

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

    public static bool TryCreateCredentials(
        string? agentIdValue,
        string? gymIdValue,
        string? secretValue,
        string? apiBaseUrlValue,
        out AgentCredentials? credentials,
        out string? error)
    {
        credentials = null;
        error = null;

        if (!Guid.TryParse(
                agentIdValue,
                out var agentId) ||
            agentId == Guid.Empty)
        {
            error =
                "O Agent ID informado é inválido.";

            return false;
        }

        if (!Guid.TryParse(
                gymIdValue,
                out var gymId) ||
            gymId == Guid.Empty)
        {
            error =
                "O Gym ID informado é inválido.";

            return false;
        }

        var secret =
            secretValue?.Trim();

        if (string.IsNullOrWhiteSpace(secret))
        {
            error =
                "O secret do Agent é obrigatório.";

            return false;
        }

        var apiBaseUrl =
            apiBaseUrlValue?.Trim();

        if (string.IsNullOrWhiteSpace(apiBaseUrl) ||
            !Uri.TryCreate(
                apiBaseUrl,
                UriKind.Absolute,
                out var apiUri))
        {
            error =
                "A URL da API é inválida.";

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

        credentials =
            new AgentCredentials(
                agentId,
                gymId,
                secret,
                apiBaseUrl.TrimEnd('/'));

        return true;
    }

    public static async Task<int> RunAsync(
        IAgentCredentialStore credentialStore,
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine(
            "Avelri Access Agent - Pareamento");
        Console.WriteLine();

        Console.Write("Agent ID: ");
        var agentId =
            Console.ReadLine();

        Console.Write("Gym ID: ");
        var gymId =
            Console.ReadLine();

        Console.Write(
            "API Base URL: ");

        var apiBaseUrl =
            Console.ReadLine();

        Console.Write(
            "Secret (não será exibido): ");

        var secret =
            ReadSecret();

        if (!TryCreateCredentials(
                agentId,
                gymId,
                secret,
                apiBaseUrl,
                out var credentials,
                out var error))
        {
            Console.WriteLine();
            Console.WriteLine(
                $"ERRO: {error}");

            return 1;
        }

        await credentialStore.SaveAsync(
            credentials!,
            cancellationToken);

        Console.WriteLine();
        Console.WriteLine(
            "Pareamento salvo com segurança via DPAPI.");
        Console.WriteLine(
            "O Access Agent já pode ser iniciado normalmente.");

        return 0;
    }

    private static string ReadSecret()
    {
        if (Console.IsInputRedirected)
        {
            return Console.ReadLine() ?? string.Empty;
        }

        var value =
            new StringBuilder();

        while (true)
        {
            var key =
                Console.ReadKey(
                    intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();

                return value.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (value.Length > 0)
                {
                    value.Length--;
                }

                continue;
            }

            if (!char.IsControl(
                    key.KeyChar))
            {
                value.Append(
                    key.KeyChar);
            }
        }
    }
}
