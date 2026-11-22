using SecondSight.Core.Session;

namespace SecondSight.Core.Chat;

/// <summary>Sending chat as the bot. Every message passes the bot's own limits and is written to the activity log.</summary>
public interface IChatChannel
{
    /// <summary>The owner's name as shown to Claude.</summary>
    string OwnerName { get; }

    Task<SessionResult> SayAsync(string text, ChatVolume volume, CancellationToken cancellationToken);

    /// <summary>Sends an IM to the owner. The bot can't IM anyone else.</summary>
    Task<SessionResult> SendOwnerImAsync(string text, CancellationToken cancellationToken);
}
