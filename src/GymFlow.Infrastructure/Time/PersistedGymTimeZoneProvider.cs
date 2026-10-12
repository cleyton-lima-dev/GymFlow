using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Validation;
namespace GymFlow.Infrastructure.Time;

public sealed class PersistedGymTimeZoneProvider(IGymSettingsRepository repository, ConfigurationGymTimeZoneProvider legacy) : IGymTimeZoneProvider
{
    private readonly Dictionary<Guid, TimeZoneInfo> _resolved = [];
    public TimeZoneInfo GetTimeZone(Guid gymId)
    {
        if (_resolved.TryGetValue(gymId, out var zone)) return zone;
        var settings = repository.Get(gymId);
        zone = settings is null ? legacy.GetTimeZone(gymId)
            : TimeZoneInfo.FindSystemTimeZoneById(GymTimeZonePolicy.ValidateIana(settings.TimeZoneId));
        _resolved.Add(gymId, zone);
        return zone;
    }
}
