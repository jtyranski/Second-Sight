using SecondSight.Core.Chat;

namespace SecondSight.Tests;

public class MessageInboxTests
{
    private static ChatMessage Msg(string text, bool owner) => new()
    {
        Time = DateTimeOffset.UnixEpoch,
        Channel = MessageChannel.LocalChat,
        FromName = owner ? "Jimmi Tiponi" : "Some One",
        FromId = "x",
        Speaker = SpeakerKind.Avatar,
        Text = text,
        FromOwner = owner,
    };

    [Fact]
    public void TakeAll_returns_and_clears_in_order()
    {
        var inbox = new MessageInbox();
        inbox.Add(Msg("a", true));
        inbox.Add(Msg("b", false));
        inbox.Add(Msg("c", true));

        var snap = inbox.TakeAll();
        Assert.Equal(["a", "c"], snap.Owner.Select(m => m.Text));
        Assert.Equal(["b"], snap.Background.Select(m => m.Text));
        Assert.True(inbox.TakeAll().IsEmpty);
    }

    [Fact]
    public void Background_can_be_left_unread()
    {
        var inbox = new MessageInbox();
        inbox.Add(Msg("a", true));
        inbox.Add(Msg("b", false));

        Assert.Empty(inbox.TakeAll(includeBackground: false).Background);
        Assert.Equal(["b"], inbox.TakeAll().Background.Select(m => m.Text));
    }

    [Fact]
    public void Background_overflow_never_pushes_out_owner_messages()
    {
        var inbox = new MessageInbox();
        inbox.Add(Msg("owner", true));
        for (var i = 0; i < MessageInbox.MaxBackgroundMessages + 5; i++) inbox.Add(Msg($"bg{i}", false));

        var snap = inbox.TakeAll();
        Assert.Equal(["owner"], snap.Owner.Select(m => m.Text));
        Assert.Equal(MessageInbox.MaxBackgroundMessages, snap.Background.Count);
        Assert.Equal(5, snap.DroppedBackground);
        Assert.Equal("bg5", snap.Background[0].Text);
    }

    [Fact]
    public async Task Wait_returns_immediately_when_owner_message_is_waiting()
    {
        var inbox = new MessageInbox();
        inbox.Add(Msg("already here", true));
        var snap = await inbox.WaitForOwnerAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.Equal("already here", snap.Owner.Single().Text);
    }

    [Fact]
    public async Task Wait_wakes_on_owner_message_but_not_background()
    {
        var inbox = new MessageInbox();
        var wait = inbox.WaitForOwnerAsync(TimeSpan.FromSeconds(30), CancellationToken.None);

        inbox.Add(Msg("chatter", false));
        await Task.Delay(100);
        Assert.False(wait.IsCompleted);

        inbox.Add(Msg("hello bot", true));
        var snap = await wait.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("hello bot", snap.Owner.Single().Text);
        Assert.Equal("chatter", snap.Background.Single().Text);
    }

    [Fact]
    public async Task Wait_times_out_with_background_only()
    {
        var inbox = new MessageInbox();
        inbox.Add(Msg("chatter", false));
        var snap = await inbox.WaitForOwnerAsync(TimeSpan.FromMilliseconds(50), CancellationToken.None);
        Assert.Empty(snap.Owner);
        Assert.Single(snap.Background);
    }

    [Fact]
    public async Task Wait_honors_cancellation()
    {
        var inbox = new MessageInbox();
        using var cts = new CancellationTokenSource(50);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => inbox.WaitForOwnerAsync(TimeSpan.FromMinutes(5), cts.Token));
    }
}
