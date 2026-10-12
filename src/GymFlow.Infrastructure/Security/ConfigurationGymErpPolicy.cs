using GymFlow.Application.Interfaces.Security;
using Microsoft.Extensions.Configuration;

namespace GymFlow.Infrastructure.Security;

// Immutable snapshot: login, JWT validation and lifecycle use the same policy
// until restart. Invalid configuration must fail startup, never enable a tenant.
public sealed class ConfigurationGymErpPolicy : IGymErpPolicy
{
    // Extend this registry alongside validation for each newly supported feature.
    private static readonly HashSet<string> RecognizedFeatures = new(StringComparer.OrdinalIgnoreCase)
    {
        "ErpEnabled"
    };
    private readonly HashSet<Guid> _enabledGyms = [];

    public ConfigurationGymErpPolicy(IConfiguration configuration)
    {
        var features = configuration.GetSection("GymFeatures");
        // The JSON provider represents an empty object as an empty string.
        if (!string.IsNullOrEmpty(features.Value))
            throw new InvalidOperationException("GymFeatures must contain tenant sections.");

        foreach (var tenant in features.GetChildren())
        {
            if (!Guid.TryParseExact(tenant.Key, "D", out var gymId) || gymId == Guid.Empty)
                throw new InvalidOperationException("GymFeatures contains an invalid GymId.");

            if (!string.IsNullOrEmpty(tenant.Value))
                throw new InvalidOperationException("Each GymFeatures tenant must contain feature settings.");

            var settings = tenant.GetChildren().ToArray();
            if (settings.Any(section => !RecognizedFeatures.Contains(section.Key)))
                throw new InvalidOperationException($"GymFeatures:{gymId:D} contains an unrecognized feature setting.");

            var enabled = settings.FirstOrDefault(section =>
                string.Equals(section.Key, "ErpEnabled", StringComparison.OrdinalIgnoreCase));
            if (enabled is null)
                continue;

            if (enabled.GetChildren().Any() ||
                !bool.TryParse(enabled.Value, out var isEnabled))
            {
                throw new InvalidOperationException(
                    $"GymFeatures:{gymId:D}:ErpEnabled must be true or false.");
            }

            if (isEnabled)
                _enabledGyms.Add(gymId);
        }
    }

    public bool IsEnabled(Guid gymId) => _enabledGyms.Contains(gymId);
}
