using System.Globalization;

namespace SecondSight.Core.Session;

/// <summary>A short rolling record of raw IMs and teleport events, for troubleshooting.</summary>
public sealed class RecentEvents(TimeProvider? time = null)
{
    public const int Capacity = 20;

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly Queue<string> _items = new();
    private readonly object _lock = new();

    public void Add(string text)
    {
        var line = string.Create(CultureInfo.InvariantCulture, $"[{_time.GetLocalNow():HH:mm:ss}] {text}");
        lock (_lock)
        {
            _items.Enqueue(line);
            while (_items.Count > Capacity) _items.Dequeue();
        }
    }

    public IReadOnlyList<string> Snapshot()
    {
        lock (_lock) return _items.ToList();
    }
}
