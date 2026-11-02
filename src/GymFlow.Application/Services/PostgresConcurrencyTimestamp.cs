namespace GymFlow.Application.Services;

internal static class PostgresConcurrencyTimestamp
{
    private const long TicksPerMicrosecond = 10;

    public static DateTime Next(DateTime utcNow, DateTime? previous)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The current timestamp must be UTC.", nameof(utcNow));
        }

        var candidateTicks = TruncateToMicrosecond(utcNow.Ticks);
        if (previous is null)
        {
            return new DateTime(candidateTicks, DateTimeKind.Utc);
        }

        // Persisted PostgreSQL timestamps have microsecond precision. Normalize
        // both operands before comparing, including DateTime values with extra ticks.
        var previousTicks = TruncateToMicrosecond(previous.Value.Ticks);
        var nextTicks = checked(previousTicks + TicksPerMicrosecond);
        return new DateTime(Math.Max(candidateTicks, nextTicks), DateTimeKind.Utc);
    }

    private static long TruncateToMicrosecond(long ticks) =>
        ticks - ticks % TicksPerMicrosecond;
}
