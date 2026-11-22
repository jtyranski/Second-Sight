using SecondSight.Core.Chat;
using SecondSight.Core.Session;
using SecondSight.Tools;

namespace SecondSight.Tests;

public class ChatToolsTests
{
    private sealed class FakeChat : IChatChannel
    {
        public List<(string Text, ChatVolume Volume)> Said = [];
        public List<string> Ims = [];
        public string OwnerName => "Jimmi Tiponi";

        public Task<SessionResult> SayAsync(string text, ChatVolume volume, CancellationToken cancellationToken)
        {
            Said.Add((text, volume));
            return Task.FromResult(new SessionResult(true, "Sent."));
        }

        public Task<SessionResult> SendOwnerImAsync(string text, CancellationToken cancellationToken)
        {
            Ims.Add(text);
            return Task.FromResult(new SessionResult(true, "Sent."));
        }
    }

    [Theory]
    [InlineData("whisper", ChatVolume.Whisper)]
    [InlineData("SHOUT", ChatVolume.Shout)]
    [InlineData("normal", ChatVolume.Normal)]
    public async Task Say_passes_volume(string volume, ChatVolume expected)
    {
        var chat = new FakeChat();
        await new ChatTools(chat, new MessageInbox()).Say("hi", volume);
        Assert.Equal(("hi", expected), chat.Said.Single());
    }

    [Fact]
    public async Task Say_rejects_unknown_volume()
    {
        var chat = new FakeChat();
        var result = await new ChatTools(chat, new MessageInbox()).Say("hi", "scream");
        Assert.Contains("Unknown volume", result);
        Assert.Empty(chat.Said);
    }

    [Fact]
    public async Task SendIm_goes_to_owner()
    {
        var chat = new FakeChat();
        await new ChatTools(chat, new MessageInbox()).SendIm("psst");
        Assert.Equal("psst", chat.Ims.Single());
    }

    [Fact]
    public async Task WaitForMessage_returns_owner_message()
    {
        var inbox = new MessageInbox();
        var tools = new ChatTools(new FakeChat(), inbox);
        var wait = tools.WaitForMessage(30);
        inbox.Add(new ChatMessage
        {
            Time = DateTimeOffset.Now, Channel = MessageChannel.LocalChat, FromName = "Jimmi Tiponi", FromId = "x",
            Speaker = SpeakerKind.Avatar, Text = "come here", FromOwner = true,
        });
        var text = await wait.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("come here", text);
    }

    [Fact]
    public void GetMessages_with_nothing_new()
    {
        Assert.Equal("No new messages from Jimmi Tiponi.", new ChatTools(new FakeChat(), new MessageInbox()).GetMessages());
    }

    private static ChatMessage FromJim(string text) => new()
    {
        Time = DateTimeOffset.Now, Channel = MessageChannel.LocalChat, FromName = "Jimmi Tiponi", FromId = "x",
        Speaker = SpeakerKind.Avatar, Text = text, FromOwner = true,
    };

    [Fact]
    public async Task ReplyAndWait_says_then_returns_the_next_message()
    {
        var chat = new FakeChat();
        var inbox = new MessageInbox();
        var wait = new ChatTools(chat, inbox).ReplyAndWait("hello Jim", timeoutSeconds: 30);
        Assert.Equal(("hello Jim", ChatVolume.Normal), chat.Said.Single());
        inbox.Add(FromJim("how are you"));
        var text = await wait.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.StartsWith("Sent. ", text);
        Assert.Contains("how are you", text);
    }

    [Fact]
    public async Task ReplyAndWait_can_answer_by_im_and_whisper()
    {
        var chat = new FakeChat();
        var inbox = new MessageInbox();
        inbox.Add(FromJim("next"));
        await new ChatTools(chat, inbox).ReplyAndWait("private", via: "IM", timeoutSeconds: 1);
        Assert.Equal("private", chat.Ims.Single());
        inbox.Add(FromJim("next"));
        await new ChatTools(chat, inbox).ReplyAndWait("quiet", volume: "whisper", timeoutSeconds: 1);
        Assert.Equal(("quiet", ChatVolume.Whisper), chat.Said.Single());
    }

    [Fact]
    public async Task ReplyAndWait_does_not_wait_when_nothing_was_sent()
    {
        var chat = new FakeChat();
        var tools = new ChatTools(chat, new MessageInbox());
        Assert.StartsWith("Not sent: unknown channel", await tools.ReplyAndWait("x", via: "group", timeoutSeconds: 300));
        Assert.StartsWith("Not sent: unknown volume", await tools.ReplyAndWait("x", volume: "scream", timeoutSeconds: 300));
        Assert.Empty(chat.Said);
        Assert.Empty(chat.Ims);
    }
}
