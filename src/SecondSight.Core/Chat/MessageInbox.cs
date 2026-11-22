namespace SecondSight.Core.Chat;

/// <summary>
/// Holds messages Claude has not read yet. Owner messages and background chat are kept
/// in separate bounded queues so background chatter can never push out the owner's words.
/// </summary>
public sealed class MessageInbox(TimeProvider time)
{
    public const int MaxOwnerMessages = 200;
    public const int MaxBackgroundMessages = 100;

    private readonly object _lock = new();
    private readonly LinkedList<ChatMessage> _owner = new();
    private readonly LinkedList<ChatMessage> _background = new();
    private TaskCompletionSource _ownerArrived = NewSignal();
    private int _droppedBackground;

    public MessageInbox() : this(TimeProvider.System) { }

    public void Add(ChatMessage message)
    {
        TaskCompletionSource? toSignal = null;
        lock (_lock)
        {
            var queue = message.FromOwner ? _owner : _background;
            queue.AddLast(message);
            var max = message.FromOwner ? MaxOwnerMessages : MaxBackgroundMessages;
            while (queue.Count > max)
            {
                queue.RemoveFirst();
                if (!message.FromOwner) _droppedBackground++;
            }
            if (message.FromOwner)
            {
                toSignal = _ownerArrived;
                _ownerArrived = NewSignal();
            }
        }
        toSignal?.TrySetResult();
    }

    /// <summary>Removes and returns everything unread, oldest first.</summary>
    public InboxSnapshot TakeAll(bool includeBackground = true)
    {
        lock (_lock)
        {
            var owner = _owner.ToList();
            _owner.Clear();
            List<ChatMessage> background = [];
            var dropped = 0;
            if (includeBackground)
            {
                background = _background.ToList();
                _background.Clear();
                dropped = _droppedBackground;
                _droppedBackground = 0;
            }
            return new InboxSnapshot(owner, background, dropped);
        }
    }

    /// <summary>
    /// Waits until the owner has said something unread, or the timeout passes,
    /// then returns everything unread.
    /// </summary>
    public async Task<InboxSnapshot> WaitForOwnerAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        Task signal;
        lock (_lock)
        {
            if (_owner.Count > 0) return TakeAll();
            signal = _ownerArrived.Task;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delay = Task.Delay(timeout, time, timeoutCts.Token);
        await Task.WhenAny(signal, delay).ConfigureAwait(false);
        timeoutCts.Cancel();
        cancellationToken.ThrowIfCancellationRequested();
        return TakeAll();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _owner.Clear();
            _background.Clear();
            _droppedBackground = 0;
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed record InboxSnapshot(IReadOnlyList<ChatMessage> Owner, IReadOnlyList<ChatMessage> Background, int DroppedBackground)
{
    public bool IsEmpty => Owner.Count == 0 && Background.Count == 0;
}
