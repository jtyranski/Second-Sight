using LibreMetaverse;

namespace SecondSight.Core.Social;

/// <summary>A friendship offer waiting for the owner to decide.</summary>
public sealed record FriendRequest(UUID AgentId, string Name, UUID SessionId, DateTimeOffset Received);

/// <summary>What the bot does with an incoming friendship offer.</summary>
public enum FriendRequestAction
{
    /// <summary>From the owner: accept right away.</summary>
    Accept,

    /// <summary>From anyone else: hold it and ask the owner.</summary>
    AskOwner,
}

/// <summary>Friendship offers from people other than the owner, held until the owner says accept or decline.</summary>
public sealed class FriendRequestBook
{
    public const int MaxPending = 50;

    private readonly object _lock = new();
    private readonly List<FriendRequest> _pending = [];

    public IReadOnlyList<FriendRequest> Pending
    {
        get { lock (_lock) return _pending.ToList(); }
    }

    /// <summary>Adds or refreshes an offer. Returns false when it replaced one already held from the same avatar.</summary>
    public bool Add(FriendRequest request)
    {
        lock (_lock)
        {
            var existing = _pending.FindIndex(r => r.AgentId == request.AgentId);
            if (existing >= 0)
            {
                _pending[existing] = request;
                return false;
            }
            _pending.Add(request);
            if (_pending.Count > MaxPending) _pending.RemoveAt(0);
            return true;
        }
    }

    public void Clear()
    {
        lock (_lock) _pending.Clear();
    }

    /// <summary>Finds a held offer by avatar name or UUID and removes it.</summary>
    public PersonMatch<FriendRequest> Take(string who)
    {
        lock (_lock)
        {
            var match = PersonMatcher.Find(_pending, who, r => r.AgentId, r => r.Name);
            if (match.Found is { } found) _pending.Remove(found);
            return match;
        }
    }
}
