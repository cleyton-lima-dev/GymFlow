using GymFlow.Application.Interfaces.Time;
using Microsoft.Extensions.Configuration;
using GymFlow.Application.Exceptions;

namespace GymFlow.Infrastructure.Time;

public class ConfigurationGymTimeZoneProvider
    : IGymTimeZoneProvider
{
    private readonly IConfiguration _configuration;

    public ConfigurationGymTimeZoneProvider(
        IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public TimeZoneInfo GetTimeZone(Guid gymId)
    {
        var timeZoneId = _configuration[
            $"GymTimeZones:{gymId:D}"];

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new GymTimeZoneNotConfiguredException(gymId);
        }

        return ResolveTimeZone(timeZoneId);
    }

    private static TimeZoneInfo ResolveTimeZone(
        string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                timeZoneId);
        }
        catch (TimeZoneNotFoundException)
            when (TimeZoneInfo.TryConvertIanaIdToWindowsId(
                timeZoneId,
                out var windowsTimeZoneId))
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                windowsTimeZoneId);
        }
    }
}