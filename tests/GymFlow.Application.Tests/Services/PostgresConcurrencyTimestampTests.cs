using GymFlow.Application.Services;

namespace GymFlow.Application.Tests.Services;

public class PostgresConcurrencyTimestampTests
{
    [Fact]
    public void Next_WhenClockValueCollidesWithinSameMicrosecond_AdvancesOnePersistedUnit()
    {
        var previous = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc)
            .AddTicks(123_450);
        var now = previous.AddTicks(9);

        var next = PostgresConcurrencyTimestamp.Next(now, previous);

        Assert.Equal(previous.AddTicks(10), next);
        Assert.Equal(0, next.Ticks % 10);
        Assert.True(next > previous);
    }

    [Fact]
    public void Next_NormalizesInputsWithSubMicrosecondPrecision()
    {
        var baseTimestamp = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
        var previous = baseTimestamp.AddTicks(17);
        var now = baseTimestamp.AddTicks(19);

        var next = PostgresConcurrencyTimestamp.Next(now, previous);

        Assert.Equal(baseTimestamp.AddTicks(20), next);
        Assert.Equal(0, next.Ticks % 10);
        Assert.True(next > new DateTime(previous.Ticks - previous.Ticks % 10, DateTimeKind.Utc));
    }

    [Fact]
    public void Next_WhenClockIsLater_UsesNormalizedClockValue()
    {
        var previous = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
        var now = previous.AddTicks(27);

        var next = PostgresConcurrencyTimestamp.Next(now, previous);

        Assert.Equal(previous.AddTicks(20), next);
        Assert.True(next > previous);
        Assert.Equal(0, next.Ticks % 10);
    }

    [Fact]
    public void Next_WithoutPrevious_NormalizesClockValue()
    {
        var now = new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc).AddTicks(7);

        var next = PostgresConcurrencyTimestamp.Next(now, null);

        Assert.Equal(new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc), next);
        Assert.Equal(0, next.Ticks % 10);
    }
}
