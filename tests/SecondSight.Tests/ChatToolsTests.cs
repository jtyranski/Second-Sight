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
}
