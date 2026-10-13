using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GymFlow.Application.DTOs.Platform;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Security;
using GymFlow.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace GymFlow.Api.Platform;

public sealed class PlatformAuthOptions
{
    public bool Enabled { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = "avelri-platform";
    public string Audience { get; init; } = "avelri-platform-web";
    public byte[] MfaEncryptionKey { get; init; } = [];
}

public static class PlatformAuthentication
{
    public const string Scheme = "Platform";
    public const string Policy = "PlatformOperatorOnly";
    public static void AddAvelriPlatform(this IServiceCollection services, IConfiguration config)
    {
        var enabled = config.GetValue<bool>("Platform:Enabled");
        byte[] mfaKey;
        try { mfaKey = enabled ? Convert.FromBase64String(config["Platform:MfaEncryptionKey"] ?? "") : []; }
        catch (FormatException) { throw new InvalidOperationException("Chave de proteção MFA inválida."); }
        var options = new PlatformAuthOptions
        {
            Enabled = enabled,
            Key = config["Platform:Jwt:Key"] ?? string.Empty,
            Issuer = config["Platform:Jwt:Issuer"] ?? "avelri-platform",
            Audience = config["Platform:Jwt:Audience"] ?? "avelri-platform-web",
            MfaEncryptionKey = mfaKey
        };
        if (options.Enabled)
        {
            if (Encoding.UTF8.GetByteCount(options.Key) < 32 || options.Key == config["Jwt:Key"] ||
                string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience) ||
                options.Issuer == config["Jwt:Issuer"] || options.Audience == config["Jwt:Audience"])
                throw new InvalidOperationException("Configure credenciais JWT exclusivas para o Platform.");
            if (mfaKey.Length != 32 || Convert.ToBase64String(mfaKey) == options.Key ||
                Convert.ToBase64String(mfaKey) == config["Jwt:Key"])
                throw new InvalidOperationException("Configure uma chave AES-256 exclusiva para proteção dos segredos MFA.");
        }
        services.AddSingleton(options);
        services.AddScoped<IPlatformSecurity, PlatformSecurity>();
        services.AddAuthentication().AddJwtBearer(Scheme, jwt =>
        {
            jwt.MapInboundClaims = false;
            jwt.TokenValidationParameters = new()
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(options.Enabled ? Encoding.UTF8.GetBytes(options.Key) : RandomNumberGenerator.GetBytes(48)),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateIssuer = true, ValidIssuer = options.Issuer,
                ValidateAudience = true, ValidAudience = options.Audience,
                ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "name", RoleClaimType = "role"
            };
            jwt.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    if (!options.Enabled || context.Principal?.FindFirst("identity_type")?.Value != "platform_operator" ||
                        context.Principal.FindFirst("amr")?.Value != "mfa" ||
                        !Guid.TryParse(context.Principal.FindFirst("sub")?.Value, out var id) ||
                        !long.TryParse(context.Principal.FindFirst("credential_version")?.Value, out var version))
                    { context.Fail("Identidade inválida."); return; }
                    var actor = await context.HttpContext.RequestServices.GetRequiredService<IPlatformRepository>().GetOperatorAsync(id);
                    if (actor is null || !actor.IsActive || actor.CredentialVersion != version)
                        context.Fail("Identidade inválida.");
                }
            };
        });
        services.AddAuthorization(auth => auth.AddPolicy(Policy, policy =>
        {
            policy.AddAuthenticationSchemes(Scheme);
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("identity_type", "platform_operator");
            policy.RequireClaim("amr", "mfa");
            policy.RequireRole("PlatformOperator");
        }));
    }
}

public sealed class PlatformSecurity(PlatformAuthOptions options) : IPlatformSecurity
{
    public string ProtectSecret(string secret)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(secret);
        var cipher = new byte[plain.Length]; var tag = new byte[16];
        using var aes = new AesGcm(options.MfaEncryptionKey, 16);
        aes.Encrypt(nonce, plain, cipher, tag, "Avelri.Platform.MFA.v1"u8);
        CryptographicOperations.ZeroMemory(plain);
        return Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }
    public long? ValidateTotp(string secret, string code, DateTimeOffset now, long lastStep)
    {
        try
        {
            var bytes = Convert.FromBase64String(secret);
            if (bytes.Length <= 28) return null;
            var plain = new byte[bytes.Length - 28];
            using var aes = new AesGcm(options.MfaEncryptionKey, 16);
            try
            {
                aes.Decrypt(bytes.AsSpan(0, 12), bytes.AsSpan(28), bytes.AsSpan(12, 16), plain, "Avelri.Platform.MFA.v1"u8);
                return PlatformTotp.Validate(Encoding.UTF8.GetString(plain), code, now, lastStep);
            }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        catch (CryptographicException) { return null; }
        catch (FormatException) { return null; }
    }
    public PlatformLoginResponse IssueToken(PlatformOperator actor, DateTimeOffset now)
    {
        if (!options.Enabled) throw new InvalidOperationException("Platform desabilitado.");
        var expires = now.UtcDateTime.AddMinutes(15);
        var token = new JwtSecurityToken(options.Issuer, options.Audience,
            [new("sub", actor.Id.ToString()), new("name", actor.Name), new("role", "PlatformOperator"),
             new("identity_type", "platform_operator"), new("amr", "mfa"),
             new("credential_version", actor.CredentialVersion.ToString(System.Globalization.CultureInfo.InvariantCulture)),
             new("jti", Guid.NewGuid().ToString())],
            notBefore: now.UtcDateTime, expires: expires,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires, actor.Name);
    }
}
