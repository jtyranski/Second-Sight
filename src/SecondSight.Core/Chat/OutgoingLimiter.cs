namespace SecondSight.Core.Chat;

/// <summary>
/// Enforces the bot's own message limits: a daily cap (counted per Second Life day, which
/// runs on Pacific time) and a per-minute rate. Each chat line or IM counts as one message.
/// </summary>
public sealed class OutgoingLimiter
{
    private static readonly TimeZoneInfo SecondLifeTime = FindPacific();

    private readonly TimeProvider _time;
    private readonly int _dailyLimit;
    private readonly int _perMinute;
    private readonly object _lock = new();
    private readonly Queue<DateTimeOffset> _recent = new();
    private DateOnly _day;
    private int _sentToday;

    public OutgoingLimiter(int dailyLimit, int perMinute, TimeProvider? time = null)
    {
        _dailyLimit = dailyLimit;
        _perMinute = perMinute;
        _time = time ?? TimeProvider.System;
        _day = Today();
    }

    public int DailyLimit => _dailyLimit;

    public int SentToday
    {
        get { lock (_lock) { Roll(); return _sentToday; } }
    }

    /// <summary>Reserves <paramref name="count"/> messages, or explains why it can't.</summary>
    public bool TryReserve(int count, out string? reason)
    {
        lock (_lock)
        {
            Roll();
            var now = _time.GetUtcNow();
            while (_recent.Count > 0 && now - _recent.Peek() >= TimeSpan.FromMinutes(1)) _recent.Dequeue();

            if (_sentToday + count > _dailyLimit)
            {
                reason = $"Daily message limit reached ({_sentToday} of {_dailyLimit} sent today, Second Life time). " +
                         "It resets at midnight Pacific time.";
                return false;
            }
            if (count > _perMinute)
            {
                reason = $"That message would need {count} chat lines, more than the limit of {_perMinute} a minute. Send something shorter.";
                return false;
            }
            if (_recent.Count + count > _perMinute)
            {
                var wait = TimeSpan.FromMinutes(1) - (now - _recent.Peek());
                reason = $"Sending too fast (limit {_perMinute} messages a minute). Try again in {Math.Ceiling(wait.TotalSeconds)} seconds.";
                return false;
            }

            _sentToday += count;
            for (var i = 0; i < count; i++) _recent.Enqueue(now);
            reason = null;
            return true;
        }
    }

    private void Roll()
    {
        var today = Today();
        if (today != _day)
        {
            _day = today;
            _sentToday = 0;
        }
    }

    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_time.GetUtcNow(), SecondLifeTime).DateTime);

    private static TimeZoneInfo FindPacific()
    {
        foreach (var id in new[] { "America/Los_Angeles", "Pacific Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("SLT", TimeSpan.FromHours(-8), "SLT", "SLT");
    }
}
