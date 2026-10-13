using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Security;
using GymFlow.Domain.Entities;
using Npgsql;

namespace GymFlow.Api.Platform;

public static class PlatformBootstrap
{
    public static async Task RunAsync(WebApplication app)
    {
        // Local, interactive and first-operator only. Never exposed as HTTP.
        if (app.Environment.IsProduction() || Console.IsInputRedirected)
            throw new InvalidOperationException("Bootstrap exige console interativo e ambiente não Production.");
        var options = app.Services.GetRequiredService<PlatformAuthOptions>();
        if (!options.Enabled) throw new InvalidOperationException("Habilite explicitamente o Platform no ambiente autorizado.");
        var target = new NpgsqlConnectionStringBuilder(app.Configuration.GetConnectionString("DefaultConnection"));
        if (target.Host is not ("127.0.0.1" or "localhost" or "::1") ||
            string.Equals(target.Database, "gymflow_dev", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(target.Database))
            throw new InvalidOperationException("Bootstrap permitido somente em banco local isolado, diferente de gymflow_dev.");
        Console.WriteLine($"Destino: {target.Host}:{target.Port}/{target.Database}. Digite o nome do banco para confirmar:");
        if (Console.ReadLine() != target.Database) throw new InvalidOperationException("Destino não confirmado.");
        using var scope = app.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPlatformRepository>();
        if (await repository.HasOperatorAsync()) throw new InvalidOperationException("O primeiro operador já foi cadastrado.");
        Console.Write("Nome: "); var name = (Console.ReadLine() ?? "").Trim();
        Console.Write("E-mail: "); var email = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
        if (name.Length is 0 or > 150 || email.Length > 200 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            throw new InvalidOperationException("Nome ou e-mail inválido.");
        Console.Write("Senha (não será exibida): "); var password = ReadSecret();
        PasswordPolicy.Validate(password);
        Console.Write("Confirme a senha: "); if (ReadSecret() != password) throw new InvalidOperationException("Senhas diferentes.");
        var secret = PlatformTotp.NewSecret();
        Console.WriteLine("Cadastre esta chave manualmente no autenticador (TOTP, 6 dígitos, 30 segundos). Exibição única; não grave a saída do console:");
        Console.WriteLine(secret);
        var code = ReadMfaCode(ReadSecret, Console.Write);
        var step = PlatformTotp.Validate(secret, code, DateTimeOffset.UtcNow, -1);
        if (step is null) throw new InvalidOperationException("MFA não confirmado. Nenhum operador criado.");
        var security = scope.ServiceProvider.GetRequiredService<IPlatformSecurity>();
        await repository.SaveOperatorAsync(new PlatformOperator
        {
            // Fixed bootstrap identity is a DB uniqueness guard, not a credential.
            Id = Guid.Parse("00000000-0000-4000-8000-000000000077"),
            Name = name, Email = email, PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash(password),
            ProtectedTotpSecret = security.ProtectSecret(secret), LastTotpStep = step.Value, CreatedAt = DateTime.UtcNow
        }, create: true);
        Console.WriteLine("Operador criado com MFA. O servidor HTTP não foi iniciado.");
    }
    // Keep malformed/empty terminal input separate from a cryptographically rejected code.
    // In particular, a buffered Enter must not submit MFA before six digits are entered.
    public static string ReadMfaCode(Func<string> readSecret, Action<string> write)
    {
        while (true)
        {
            write("Código do autenticador (6 dígitos): ");
            var code = readSecret().Trim();
            if (code.Length == 6 && code.All(c => c is >= '0' and <= '9')) return code;
            write("Entrada inválida. Digite os seis dígitos do autenticador.\n");
        }
    }
    private static string ReadSecret()
    {
        var value = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return value.ToString(); }
            if (key.Key == ConsoleKey.Backspace) { if (value.Length > 0) value.Length--; }
            else if (!char.IsControl(key.KeyChar) && value.Length < 128) value.Append(key.KeyChar);
        }
    }
}
