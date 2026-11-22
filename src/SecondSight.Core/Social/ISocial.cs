using SecondSight.Core.Session;

namespace SecondSight.Core.Social;

/// <summary>Home location and friendships. Changes happen only when the owner asks.</summary>
public interface ISocial
{
    /// <summary>Makes the bot's current spot its home location.</summary>
    Task<SessionResult> SetHomeAsync(CancellationToken cancellationToken);

    SessionResult ListFriends();

    Task<SessionResult> RespondToFriendRequestAsync(string who, bool accept, CancellationToken cancellationToken);

    /// <summary>Sends the owner a friendship offer.</summary>
    Task<SessionResult> OfferFriendshipToOwnerAsync(CancellationToken cancellationToken);

    Task<SessionResult> RemoveFriendAsync(string who, CancellationToken cancellationToken);
}
