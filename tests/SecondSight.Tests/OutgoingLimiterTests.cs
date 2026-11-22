using SecondSight.Core.Chat;

namespace SecondSight.Tests;

public class OutgoingLimiterTests
{
    // 2026-11-22 10:00 Pacific (PST, UTC-8)
    private static readonly DateTimeOffset Start = new(2026, 11, 22, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Enforces_per_minute_rate()
    {
        var time = new ManualTime(Start);
        var limiter = new OutgoingLimiter(dailyLimit: 500, perMinute: 3, time);

        Assert.True(limiter.TryReserve(2, out _));
        Assert.True(limiter.TryReserve(1, out _));
        Assert.False(limiter.TryReserve(1, out var reason));
        Assert.Contains("too fast", reason);

        time.Advance(TimeSpan.FromSeconds(61));
        Assert.True(limiter.TryReserve(1, out _));
    }

    [Fact]
    public void Enforces_daily_cap_and_resets_at_pacific_midnight()
    {
        var time = new ManualTime(Start);
        var limiter = new OutgoingLimiter(dailyLimit: 2, perMinute: 100, time);

        Assert.True(limiter.TryReserve(2, out _));
        Assert.False(limiter.TryReserve(1, out var reason));
        Assert.Contains("Daily message limit", reason);
        Assert.Equal(2, limiter.SentToday);

        // 7:59 UTC next day is still 11:59 PM Pacific on the 22nd.
        time.Advance(new DateTimeOffset(2026, 11, 23, 7, 59, 0, TimeSpan.Zero) - Start);
        Assert.False(limiter.TryReserve(1, out _));

        time.Advance(TimeSpan.FromMinutes(2));
        Assert.True(limiter.TryReserve(1, out _));
        Assert.Equal(1, limiter.SentToday);
    }

    [Fact]
    public void Failed_reservation_uses_nothing()
    {
        var limiter = new OutgoingLimiter(dailyLimit: 5, perMinute: 2, new ManualTime(Start));
        Assert.False(limiter.TryReserve(3, out _));
        Assert.Equal(0, limiter.SentToday);
        Assert.True(limiter.TryReserve(2, out _));
    }
}
