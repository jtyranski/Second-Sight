using LibreMetaverse;
using SecondSight.Core;
using SecondSight.Core.Chat;

namespace SecondSight.Tests;

public class MessageClassifierTests
{
    private static readonly UUID Self = UUID.Random();
    private static readonly UUID Jim = UUID.Random();
    private static readonly UUID Stranger = UUID.Random();
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 18, 0, 0, TimeSpan.Zero);

    private static OwnerIdentity Owner(bool resolved = true) =>
        new(AvatarName.Parse("Jimmi Tiponi")) { Id = resolved ? Jim : null };

    private static ChatMessage? Local(UUID from, string name, string text = "hi",
        ChatType type = ChatType.Normal, ChatSourceType source = ChatSourceType.Agent,
        ChatAudibleLevel audible = ChatAudibleLevel.Fully, OwnerIdentity? owner = null) =>
        MessageClassifier.FromLocalChat(type, source, audible, name, from, new Vector3(13, 14, 22), text,
            Self, new Vector3(10, 10, 22), owner ?? Owner(), Now);

    [Fact]
    public void Owner_local_chat_is_actionable_with_distance()
    {
        var m = Local(Jim, "Jimmi Tiponi", "follow me")!;
        Assert.True(m.FromOwner);
        Assert.Equal(MessageChannel.LocalChat, m.Channel);
        Assert.Equal(5.0, (double)m.Distance!.Value, 2);
        Assert.Equal("follow me", m.Text);
    }

    [Fact]
    public void Other_avatars_are_background()
    {
        var m = Local(Stranger, "Some One")!;
        Assert.False(m.FromOwner);
        Assert.Equal(SpeakerKind.Avatar, m.Speaker);
    }

    [Fact]
    public void Object_using_owner_name_is_not_the_owner()
    {
        var m = Local(Stranger, "Jimmi Tiponi", source: ChatSourceType.Object, owner: Owner(resolved: false))!;
        Assert.False(m.FromOwner);
        Assert.Equal(SpeakerKind.Object, m.Speaker);
    }

    [Fact]
    public void Name_match_is_used_only_until_uuid_is_known()
    {
        Assert.True(Local(Stranger, "Jimmi Tiponi", owner: Owner(resolved: false))!.FromOwner);
        Assert.False(Local(Stranger, "Jimmi Tiponi", owner: Owner(resolved: true))!.FromOwner);
    }

    [Theory]
    [InlineData(ChatType.StartTyping)]
    [InlineData(ChatType.StopTyping)]
    [InlineData(ChatType.Debug)]
    public void Typing_and_debug_are_dropped(ChatType type) =>
        Assert.Null(Local(Jim, "Jimmi Tiponi", type: type));

    [Fact]
    public void Own_chat_inaudible_and_empty_lines_are_dropped()
    {
        Assert.Null(Local(Self, "DexterFrost Resident"));
        Assert.Null(Local(Jim, "Jimmi Tiponi", audible: ChatAudibleLevel.Not));
        Assert.Null(Local(Jim, "Jimmi Tiponi", text: "  "));
    }

    [Theory]
    [InlineData(ChatType.Whisper, ChatVolume.Whisper)]
    [InlineData(ChatType.Shout, ChatVolume.Shout)]
    public void Volume_is_kept(ChatType type, ChatVolume expected) =>
        Assert.Equal(expected, Local(Jim, "Jimmi Tiponi", type: type)!.Volume);

    private static InstantMessage Im(UUID from, string name, string text = "hey",
        InstantMessageDialog dialog = InstantMessageDialog.MessageFromAgent, bool group = false) => new()
    {
        FromAgentID = from, FromAgentName = name, Message = text, Dialog = dialog, GroupIM = group, ToAgentID = Self,
    };

    [Fact]
    public void Owner_im_is_kept()
    {
        var m = MessageClassifier.FromInstantMessage(Im(Jim, "Jimmi Tiponi"), Self, Owner(), Now)!;
        Assert.True(m.FromOwner);
        Assert.Equal(MessageChannel.InstantMessage, m.Channel);
    }

    [Fact]
    public void Ims_from_others_groups_and_offers_are_dropped()
    {
        Assert.Null(MessageClassifier.FromInstantMessage(Im(Stranger, "Some One"), Self, Owner(), Now));
        Assert.Null(MessageClassifier.FromInstantMessage(Im(Jim, "Jimmi Tiponi", group: true), Self, Owner(), Now));
        Assert.Null(MessageClassifier.FromInstantMessage(Im(Jim, "Jimmi Tiponi", dialog: InstantMessageDialog.InventoryOffered), Self, Owner(), Now));
        Assert.Null(MessageClassifier.FromInstantMessage(Im(Jim, "Jimmi Tiponi", dialog: InstantMessageDialog.FriendshipOffered), Self, Owner(), Now));
    }

    [Fact]
    public void Owner_identity_from_config()
    {
        var byId = OwnerIdentity.FromConfig(Jim.ToString());
        Assert.Equal(Jim, byId.Id);
        Assert.Null(byId.Name);

        var byName = OwnerIdentity.FromConfig("Jimmi Tiponi");
        Assert.Null(byName.Id);
        Assert.Equal("Jimmi Tiponi", byName.Name!.DisplayName);
    }
}
