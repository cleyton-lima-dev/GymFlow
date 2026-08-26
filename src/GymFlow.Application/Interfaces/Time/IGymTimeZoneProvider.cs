namespace GymFlow.Application.Interfaces.Time;

public interface IGymTimeZoneProvider
{
    TimeZoneInfo GetTimeZone(Guid gymId);
}