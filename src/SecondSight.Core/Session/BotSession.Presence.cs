using LibreMetaverse;
using Microsoft.Extensions.Logging;

namespace SecondSight.Core.Session;

/// <summary>
/// Small things the bot does on its own so a conversation feels responsive while Claude is thinking:
/// it turns toward the owner and shows the typing animation until it answers.
/// </summary>
public sealed partial class BotSession
{
    /// <summary>How long the typing animation runs if Claude never answers.</summary>
    public static readonly TimeSpan TypingTimeout = TimeSpan.FromSeconds(45);

    private readonly object _typingLock = new();
    private CancellationTokenSource? _typingTimer;

    /// <summary>The owner just spoke in local chat: face them and start "typing".</summary>
    private void ReactToOwner(GridClient client, Vector3 ownerPosition)
    {
        try
        {
            if (client.Self.SittingOn == 0 && ownerPosition != Vector3.Zero)
                client.Self.Movement.TurnToward(ownerPosition, true);
            StartTyping(client);
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "Couldn't react to the owner");
        }
    }

    private void StartTyping(GridClient client)
    {
        CancellationTokenSource timer;
        lock (_typingLock)
        {
            var alreadyTyping = _typingTimer != null;
            _typingTimer?.Cancel();
            _typingTimer = timer = new CancellationTokenSource();
            if (!alreadyTyping)
            {
                client.Self.Chat(string.Empty, 0, ChatType.StartTyping, false);
                client.Self.AnimationStart(Animations.TYPE, false);
            }
        }
        _ = StopTypingLaterAsync(client, timer);
    }

    private async Task StopTypingLaterAsync(GridClient client, CancellationTokenSource timer)
    {
        try
        {
            await Task.Delay(TypingTimeout, _time, timer.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        lock (_typingLock)
        {
            if (_typingTimer != timer) return;
        }
        StopTyping(client);
    }

    /// <summary>Stops the typing animation, if it is running. Called before the bot says anything.</summary>
    private void StopTyping(GridClient client)
    {
        lock (_typingLock)
        {
            if (_typingTimer == null) return;
            _typingTimer.Cancel();
            _typingTimer = null;
        }
        try
        {
            client.Self.Chat(string.Empty, 0, ChatType.StopTyping, false);
            client.Self.AnimationStop(Animations.TYPE, false);
        }
        catch (Exception e)
        {
            _logger.LogDebug(e, "Couldn't stop typing");
        }
    }
}
