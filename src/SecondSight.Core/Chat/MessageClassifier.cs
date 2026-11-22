using LibreMetaverse;

namespace SecondSight.Core.Chat;

/// <summary>
/// Decides which incoming chat and IMs reach Claude, and how they are labeled.
/// Owner messages are actionable. Other people's local chat is background only.
/// IMs from anyone but the owner are dropped.
/// </summary>
public static class MessageClassifier
{
    public static ChatMessage? FromLocalChat(
        ChatType type, ChatSourceType source, ChatAudibleLevel audible,
        string fromName, UUID sourceId, Vector3 position, string text,
        UUID selfId, Vector3 selfPosition, OwnerIdentity owner, DateTimeOffset now)
    {
        if (sourceId == selfId) return null;
        if (audible == ChatAudibleLevel.Not) return null;
        if (string.IsNullOrWhiteSpace(text)) return null;

        ChatVolume volume;
        switch (type)
        {
            case ChatType.Whisper: volume = ChatVolume.Whisper; break;
            case ChatType.Normal: volume = ChatVolume.Normal; break;
            case ChatType.Shout: volume = ChatVolume.Shout; break;
            case ChatType.RegionSay:
            case ChatType.RegionSayTo:
            case ChatType.OwnerSay: volume = ChatVolume.Normal; break;
            default: return null; // typing indicators, debug channel
        }

        var speaker = source switch
        {
            ChatSourceType.Agent => SpeakerKind.Avatar,
            ChatSourceType.Object => SpeakerKind.Object,
            _ => SpeakerKind.System,
        };

        return new ChatMessage
        {
            Time = now,
            Channel = MessageChannel.LocalChat,
            FromName = string.IsNullOrWhiteSpace(fromName) ? "(unknown)" : fromName,
            FromId = sourceId.ToString(),
            Speaker = speaker,
            Text = text,
            FromOwner = owner.IsOwner(sourceId, fromName, speaker == SpeakerKind.Avatar),
            Volume = volume,
            Distance = position == Vector3.Zero ? null : Vector3.Distance(position, selfPosition),
        };
    }

    public static ChatMessage? FromInstantMessage(InstantMessage im, UUID selfId, OwnerIdentity owner, DateTimeOffset now)
    {
        if (im.FromAgentID == selfId) return null;
        if (im.Dialog != InstantMessageDialog.MessageFromAgent) return null;
        if (im.GroupIM) return null;
        if (string.IsNullOrWhiteSpace(im.Message)) return null;
        if (!owner.IsOwner(im.FromAgentID, im.FromAgentName, isAvatar: true)) return null;

        return new ChatMessage
        {
            Time = now,
            Channel = MessageChannel.InstantMessage,
            FromName = im.FromAgentName,
            FromId = im.FromAgentID.ToString(),
            Speaker = SpeakerKind.Avatar,
            Text = im.Message,
            FromOwner = true,
        };
    }
}
