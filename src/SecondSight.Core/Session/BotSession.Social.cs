using LibreMetaverse;
using Microsoft.Extensions.Logging;
using SecondSight.Core.Social;

namespace SecondSight.Core.Session;

/// <summary>Home location and friendships.</summary>
public sealed partial class BotSession : ISocial
{
    private static readonly TimeSpan SetHomeWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FriendshipReplyTimeout = TimeSpan.FromSeconds(10);

    private readonly FriendRequestBook _friendRequests = new();

    /// <summary>Friend requests waiting for the owner to decide.</summary>
    public IReadOnlyList<FriendRequest> PendingFriendRequests => _friendRequests.Pending;

    private void AttachSocial(GridClient client)
    {
        _friendRequests.Clear();
        client.Friends.FriendshipResponse += OnFriendshipResponse;
        client.Friends.FriendshipTerminated += OnFriendshipTerminated;
    }

    private void DetachSocial(GridClient client)
    {
        client.Friends.FriendshipResponse -= OnFriendshipResponse;
        client.Friends.FriendshipTerminated -= OnFriendshipTerminated;
    }

    /// <summary>The owner's offers are accepted; anyone else's are held and the owner is asked.</summary>
    public static FriendRequestAction DecideFriendRequest(UUID from, string? name, Chat.OwnerIdentity owner) =>
        owner.IsOwner(from, name, isAvatar: true) ? FriendRequestAction.Accept : FriendRequestAction.AskOwner;

    /// <summary>
    /// Handles a friendship offer IM. <paramref name="offline"/> is true when the offer was stored while the bot
    /// was logged out (it arrives without a simulator); Second Life wants those answered through a capability.
    /// </summary>
    private void HandleFriendOffer(GridClient client, InstantMessage im, bool offline)
    {
        var name = FriendsFormatter.DisplayName(im.FromAgentName);
        _recent.Add($"Friendship offer from {im.FromAgentName} ({im.FromAgentID}){(offline ? " [offline]" : "")}");

        if (DecideFriendRequest(im.FromAgentID, im.FromAgentName, _owner) == FriendRequestAction.Accept)
        {
            _activity.Write("friend-accept", $"Accepted friendship offer from {name}");
            AddEvent($"{name} sent a friend request. Accepted it.");
            _ = RespondAsync(client, im.FromAgentID, im.IMSessionID, offline, accept: true);
            return;
        }

        var isNew = _friendRequests.Add(new FriendRequest(im.FromAgentID, im.FromAgentName, im.IMSessionID, _time.GetLocalNow(), offline));
        _activity.Write("friend-request", $"Friend request from {name}; waiting for {OwnerName}");
        AddEvent($"Friend request from {name}. Don't accept or decline it yourself: ask {OwnerName}, " +
                 "then use respond_friend_request with their answer.");
        if (isNew) TellOwner(client, $"Friend request from {name}. Tell me whether to accept or decline it.");
    }

    private void OnFriendshipResponse(object? sender, FriendshipResponseEventArgs e)
    {
        var name = FriendsFormatter.DisplayName(e.AgentName);
        _activity.Write("friend-response", $"{name} {(e.Accepted ? "accepted" : "declined")} the bot's friend request");
        if (e.AgentID == _owner.Id)
            AddEvent($"{name} {(e.Accepted ? "accepted" : "declined")} the bot's friend request.");
    }

    private void OnFriendshipTerminated(object? sender, FriendshipTerminatedEventArgs e)
    {
        var name = FriendsFormatter.DisplayName(e.AgentName);
        _activity.Write("friend-removed", $"Friendship with {name} ended");
        if (e.AgentID == _owner.Id) AddEvent($"{name} removed the bot from their friends.");
    }

    /// <summary>Sends the owner a short IM about something that needs their decision. Counts toward the message limits.</summary>
    private void TellOwner(GridClient client, string text)
    {
        if (_owner.Id is not { } ownerId) return;
        if (!_limiter.TryReserve(1, out var reason))
        {
            _logger.LogWarning("Couldn't IM the owner: {Reason}", reason);
            return;
        }
        client.Self.InstantMessage(ownerId, text);
        _activity.Write("im-to-owner", text);
    }

    /// <summary>
    /// Answers a friendship offer. A live offer is answered with the offer's IM session, like a normal viewer does.
    /// An offer delivered at login is answered through the AcceptFriendship/DeclineFriendship capability when the
    /// region has it. (LibreMetaverse's capability calls only log failures, so the live path must not depend on them.)
    /// </summary>
    private async Task RespondAsync(GridClient client, UUID agent, UUID session, bool offline, bool accept)
    {
        var capName = accept ? "AcceptFriendship" : "DeclineFriendship";
        var useCap = offline && client.Network.CurrentSim?.Caps?.CapabilityURI(capName) != null;
        _recent.Add($"  -> {(accept ? "accept" : "decline")} friendship from {agent} via {(useCap ? capName + " capability" : "session " + session)}");
        try
        {
            if (useCap)
            {
                using var timeout = new CancellationTokenSource(FriendshipReplyTimeout);
                if (accept) await client.Friends.AcceptFriendshipViaCapAsync(agent, timeout.Token);
                else await client.Friends.DeclineFriendshipViaCapAsync(agent, timeout.Token);
            }
            else if (accept) client.Friends.AcceptFriendship(agent, session);
            else client.Friends.DeclineFriendship(agent, session);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Answering the friendship offer from {Agent} failed", agent);
            _recent.Add($"  -> failed: {e.Message}");
        }
    }

    public async Task<SessionResult> SetHomeAsync(CancellationToken cancellationToken)
    {
        if (!TryGetClient(out var client, out var notLoggedIn)) return notLoggedIn;
        var where = $"{client.Network.CurrentSim?.Name} ({client.Self.SimPosition.X:0}, {client.Self.SimPosition.Y:0}, {client.Self.SimPosition.Z:0})";

        var alert = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnAlert(object? sender, AlertMessageEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Message)) alert.TrySetResult(e.Message.Trim());
        }
        client.Self.AlertMessage += OnAlert;
        try
        {
            client.Self.SetHome();
            var done = await Task.WhenAny(alert.Task, Task.Delay(SetHomeWait, cancellationToken));
            if (done != alert.Task)
            {
                _activity.Write("set-home", $"Asked to set home at {where}; no reply");
                return new(true, $"Asked the region to make {where} the bot's home. It didn't confirm; " +
                    "teleport_home will show whether it worked.");
            }
            var reply = alert.Task.Result;
            _activity.Write("set-home", $"{where}: {reply}");
            return new(SetHomeSucceeded(reply), $"Set home at {where}. The region said: \"{reply}\"");
        }
        finally
        {
            client.Self.AlertMessage -= OnAlert;
        }
    }

    /// <summary>Second Life answers "Home position set." on success, and explains where you can set home otherwise.</summary>
    public static bool SetHomeSucceeded(string reply) =>
        reply.Contains("home position set", StringComparison.OrdinalIgnoreCase)
        || reply.Contains("home location set", StringComparison.OrdinalIgnoreCase);

    public SessionResult ListFriends()
    {
        if (!TryGetClient(out var client, out var notLoggedIn)) return notLoggedIn;
        var friends = client.Friends.FriendList.Values
            .Select(f => new Friend(f.UUID, string.IsNullOrWhiteSpace(f.Name) ? NameOf(f.UUID) : f.Name, f.IsOnline, f.CanSeeThemOnline))
            .ToList();
        var missing = friends.Where(f => f.Name == null).Select(f => f.Id).ToList();
        if (missing.Count > 0) client.Avatars.RequestAvatarNames(missing);
        return new(true, FriendsFormatter.List(friends, _owner.Id, _friendRequests.Pending));
    }

    public async Task<SessionResult> RespondToFriendRequestAsync(string who, bool accept, CancellationToken cancellationToken)
    {
        if (!TryGetClient(out var client, out var notLoggedIn)) return notLoggedIn;
        var match = _friendRequests.Take(who);
        if (match.Found is not { } request)
        {
            if (match.Ambiguous) return new(false, FriendsFormatter.Ambiguous(who, match, r => r.Name));
            return new(false, $"No friend request from \"{who}\" is waiting. {FriendsFormatter.Pending(_friendRequests.Pending).TrimEnd()}");
        }

        await RespondAsync(client, request.AgentId, request.SessionId, request.Offline, accept);
        var name = FriendsFormatter.DisplayName(request.Name);
        _activity.Write(accept ? "friend-accept" : "friend-decline", $"{(accept ? "Accepted" : "Declined")} friend request from {name}");
        return new(true, $"{(accept ? "Accepted" : "Declined")} the friend request from {name}.");
    }

    public Task<SessionResult> OfferFriendshipToOwnerAsync(CancellationToken cancellationToken)
    {
        if (!TryGetClient(out var client, out var notLoggedIn)) return Task.FromResult(notLoggedIn);
        if (_owner.Id is not { } ownerId)
            return Task.FromResult(new SessionResult(false, $"Can't send {OwnerName} a friend request: the owner's UUID isn't known yet."));
        if (client.Friends.FriendList.ContainsKey(ownerId))
            return Task.FromResult(new SessionResult(true, $"{OwnerName} is already the bot's friend."));
        if (!_limiter.TryReserve(1, out var reason)) return Task.FromResult(new SessionResult(false, reason!));

        client.Friends.OfferFriendship(ownerId, "Second Sight bot: friend request for my owner.");
        _activity.Write("friend-offer", $"Sent friend request to {OwnerName}");
        return Task.FromResult(new SessionResult(true, $"Sent {OwnerName} a friend request. They need to accept it in their viewer."));
    }

    public Task<SessionResult> RemoveFriendAsync(string who, CancellationToken cancellationToken)
    {
        if (!TryGetClient(out var client, out var notLoggedIn)) return Task.FromResult(notLoggedIn);
        var friends = client.Friends.FriendList.Values.ToList();
        var match = PersonMatcher.Find(friends, who, f => f.UUID, f => string.IsNullOrWhiteSpace(f.Name) ? NameOf(f.UUID) : f.Name);
        if (match.Found is not { } friend)
            return Task.FromResult(new SessionResult(false, match.Ambiguous
                ? FriendsFormatter.Ambiguous(who, match, f => f.Name)
                : $"\"{who}\" isn't on the bot's friends list. Use list_friends to see who is."));

        client.Friends.TerminateFriendship(friend.UUID);
        var name = FriendsFormatter.DisplayName(friend.Name ?? NameOf(friend.UUID));
        _activity.Write("friend-remove", $"Removed {name} from friends");
        return Task.FromResult(new SessionResult(true, $"Removed {name} from the bot's friends."));
    }
}
