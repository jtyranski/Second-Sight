namespace SecondSight.Core.Chat;

public enum MessageChannel { LocalChat, InstantMessage }

public enum ChatVolume { Whisper, Normal, Shout }

public enum SpeakerKind { Avatar, Object, System }

/// <summary>A chat line or IM the bot received, already filtered and classified.</summary>
public sealed record ChatMessage
{
    public required DateTimeOffset Time { get; init; }
    public required MessageChannel Channel { get; init; }
    public required string FromName { get; init; }
    public required string FromId { get; init; }
    public required SpeakerKind Speaker { get; init; }
    public required string Text { get; init; }

    /// <summary>True when the owner sent it. Only owner messages are things Claude should act on or answer.</summary>
    public required bool FromOwner { get; init; }

    public ChatVolume Volume { get; init; } = ChatVolume.Normal;

    /// <summary>Distance from the bot in meters, for local chat.</summary>
    public float? Distance { get; init; }
}
