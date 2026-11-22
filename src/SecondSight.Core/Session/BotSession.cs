using LibreMetaverse;
using Microsoft.Extensions.Logging;
using SecondSight.Core.Chat;
using SecondSight.Core.Config;

namespace SecondSight.Core.Session;

/// <summary>Owns the LibreMetaverse <see cref="GridClient"/>: login/logout, incoming chat, and sending chat.</summary>
public sealed class BotSession : IBotSession, IChatChannel, IAsyncDisposable
{
    public const string Channel = "SecondSight";
    public static readonly string Version = typeof(BotSession).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    private static readonly TimeSpan OwnerLookupTimeout = TimeSpan.FromSeconds(15);

    private readonly BotConfig _config;
    private readonly ILogger<BotSession> _logger;
    private readonly MessageInbox _inbox;
    private readonly OutgoingLimiter _limiter;
    private readonly ActivityLog _activity;
    private readonly TimeProvider _time;
    private readonly OwnerIdentity _owner;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private GridClient? _client;
    private volatile ConnectionState _state = ConnectionState.LoggedOut;
    private string? _lastError;

    public BotSession(BotConfig config, MessageInbox inbox, OutgoingLimiter limiter, ActivityLog activity,
        ILogger<BotSession> logger, TimeProvider? time = null)
    {
        _config = config;
        _inbox = inbox;
        _limiter = limiter;
        _activity = activity;
        _logger = logger;
        _time = time ?? TimeProvider.System;
        _owner = OwnerIdentity.FromConfig(config.Owner);
    }

    public ConnectionState State => _state;

    /// <summary>The owner's UUID once resolved. Chat and IMs are filtered by it.</summary>
    public UUID? OwnerId => _owner.Id;

    public string OwnerName => _owner.Name?.DisplayName ?? _config.Owner ?? "your owner";

    public async Task<SessionResult> LoginAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_state == ConnectionState.LoggedIn && _client?.Network.Connected == true)
                return new(true, $"Already logged in. {GetStatus().Describe()}");

            var problems = _config.Validate();
            if (problems.Count > 0)
                return Fail("The bot is not configured: " + string.Join(" ", problems));

            var name = AvatarName.Parse(_config.Username!);
            StartLocationParser.TryParse(_config.StartLocation, out var start, out _);

            if (_client is { } stale)
            {
                // Left over from a dropped connection.
                Detach(stale);
                await stale.DisposeAsync();
                _client = null;
            }

            _state = ConnectionState.LoggingIn;
            _lastError = null;

            var client = new GridClient();
            client.Network.Disconnected += OnDisconnected;

            var loginParams = client.Network.DefaultLoginParams(name.First, name.Last, _config.Password!, Channel, Version);
            loginParams.Start = start;
            if (!string.IsNullOrWhiteSpace(_config.LoginUri)) loginParams.URI = _config.LoginUri;

            _logger.LogInformation("Logging in {Name} at {Start}", name.FullName, start);
            bool ok;
            try
            {
                ok = await client.Network.LoginAsync(loginParams, cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _logger.LogError(e, "Login threw");
                ok = false;
                _lastError = e.Message;
            }

            if (!ok)
            {
                var reason = client.Network.LoginMessage;
                if (string.IsNullOrWhiteSpace(reason)) reason = _lastError ?? "unknown error";
                client.Network.Disconnected -= OnDisconnected;
                await client.DisposeAsync();
                _state = ConnectionState.LoggedOut;
                return Fail($"Login failed: {reason}");
            }

            _inbox.Clear();
            client.Self.ChatFromSimulator += OnChat;
            client.Self.IM += OnInstantMessage;
            _client = client;
            _state = ConnectionState.LoggedIn;
            _logger.LogInformation("Logged in to {Region}", client.Network.CurrentSim?.Name);
            _activity.Write("login", $"Logged in to {client.Network.CurrentSim?.Name}");

            var ownerNote = await ResolveOwnerAsync(client, cancellationToken);
            return new(true, $"{GetStatus().Describe()}{ownerNote}");
        }
        catch (OperationCanceledException)
        {
            _state = ConnectionState.LoggedOut;
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<SessionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var client = _client;
            if (client == null || _state == ConnectionState.LoggedOut)
                return new(true, "Already logged out.");

            _state = ConnectionState.LoggingOut;
            Detach(client);
            try
            {
                if (client.Network.Connected) await client.Network.LogoutAsync(cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _logger.LogWarning(e, "Logout did not complete cleanly");
            }
            finally
            {
                await client.DisposeAsync();
                _client = null;
                _state = ConnectionState.LoggedOut;
                _activity.Write("logout", "Logged out");
            }
            return new(true, "Logged out.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public BotStatus GetStatus()
    {
        var client = _client;
        var ownerName = string.IsNullOrWhiteSpace(_config.Owner) ? null : DescribeOwner(_config.Owner);
        var ownerId = _owner.Id?.ToString();

        if (client == null || _state != ConnectionState.LoggedIn)
            return new BotStatus
            {
                State = _state, OwnerName = ownerName, OwnerId = ownerId, LastError = _lastError,
                MessagesSentToday = _limiter.SentToday, DailyMessageLimit = _limiter.DailyLimit,
            };

        var self = client.Self;
        var pos = self.SimPosition;
        return new BotStatus
        {
            State = ConnectionState.LoggedIn,
            AvatarName = self.Name,
            Region = client.Network.CurrentSim?.Name,
            Position = (pos.X, pos.Y, pos.Z),
            Sitting = self.SittingOn != 0,
            Flying = self.Movement.Fly,
            OwnerName = ownerName,
            OwnerId = ownerId,
            LastError = _lastError,
            MessagesSentToday = _limiter.SentToday,
            DailyMessageLimit = _limiter.DailyLimit,
        };
    }

    public Task<SessionResult> SayAsync(string text, ChatVolume volume, CancellationToken cancellationToken)
    {
        var type = volume switch
        {
            ChatVolume.Whisper => ChatType.Whisper,
            ChatVolume.Shout => ChatType.Shout,
            _ => ChatType.Normal,
        };
        return SendAsync(text, "said", (client, chunk) => client.Self.Chat(chunk, 0, type, false));
    }

    public Task<SessionResult> SendOwnerImAsync(string text, CancellationToken cancellationToken)
    {
        if (_client == null || _state != ConnectionState.LoggedIn)
            return Task.FromResult(new SessionResult(false, "The bot is not logged in. Use the login tool first."));
        if (_owner.Id is not { } ownerId)
            return Task.FromResult(new SessionResult(false, $"Can't IM {OwnerName}: the owner's UUID isn't known yet. Check the Owner setting."));
        return SendAsync(text, "im-to-owner", (client, chunk) => client.Self.InstantMessage(ownerId, chunk));
    }

    private Task<SessionResult> SendAsync(string text, string kind, Action<GridClient, string> send)
    {
        var client = _client;
        if (client == null || _state != ConnectionState.LoggedIn || !client.Network.Connected)
            return Task.FromResult(new SessionResult(false, "The bot is not logged in. Use the login tool first."));
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult(new SessionResult(false, "Nothing to send: the message is empty."));

        var chunks = MessageSplitter.Split(text);
        if (!_limiter.TryReserve(chunks.Count, out var reason))
        {
            _activity.Write("blocked", $"{kind}: {reason}");
            return Task.FromResult(new SessionResult(false, reason!));
        }

        foreach (var chunk in chunks)
        {
            send(client, chunk);
            _activity.Write(kind, chunk);
        }
        var note = chunks.Count > 1 ? $" (sent as {chunks.Count} messages)" : "";
        return Task.FromResult(new SessionResult(true, $"Sent{note}. {_limiter.SentToday} of {_limiter.DailyLimit} messages used today."));
    }

    private void OnChat(object? sender, ChatEventArgs e)
    {
        var client = _client;
        if (client == null) return;
        var message = MessageClassifier.FromLocalChat(e.Type, e.SourceType, e.AudibleLevel, e.FromName, e.SourceID,
            e.Position, e.Message, client.Self.AgentID, client.Self.SimPosition, _owner, _time.GetLocalNow());
        if (message == null) return;
        _inbox.Add(message);
        if (message.FromOwner) _activity.Write("heard-owner", message.Text);
    }

    private void OnInstantMessage(object? sender, InstantMessageEventArgs e)
    {
        var client = _client;
        if (client == null) return;
        var message = MessageClassifier.FromInstantMessage(e.IM, client.Self.AgentID, _owner, _time.GetLocalNow());
        if (message == null)
        {
            if (e.IM.Dialog == InstantMessageDialog.MessageFromAgent && e.IM.FromAgentID != client.Self.AgentID)
                _logger.LogInformation("Ignored IM from {Name} (not the owner)", e.IM.FromAgentName);
            return;
        }
        _inbox.Add(message);
        _activity.Write("im-from-owner", message.Text);
    }

    private void Detach(GridClient client)
    {
        client.Network.Disconnected -= OnDisconnected;
        client.Self.ChatFromSimulator -= OnChat;
        client.Self.IM -= OnInstantMessage;
    }

    private static string DescribeOwner(string owner) =>
        UUID.TryParse(owner, out _) ? owner : AvatarName.Parse(owner).DisplayName;

    /// <summary>Looks up the owner's UUID by name, unless the config already gives a UUID.</summary>
    private async Task<string> ResolveOwnerAsync(GridClient client, CancellationToken cancellationToken)
    {
        if (_owner.Id != null) return "";

        var owner = AvatarName.Parse(_config.Owner!);
        var queryId = UUID.Random();
        var tcs = new TaskCompletionSource<UUID?>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnReply(object? sender, AvatarPickerReplyEventArgs e)
        {
            if (e.QueryID != queryId) return;
            var match = e.Avatars.FirstOrDefault(kv => owner.Matches(kv.Value));
            tcs.TrySetResult(match.Value != null ? match.Key : null);
        }

        client.Avatars.AvatarPickerReply += OnReply;
        try
        {
            client.Avatars.RequestAvatarNameSearch(owner.FullName, queryId);
            var finished = await Task.WhenAny(tcs.Task, Task.Delay(OwnerLookupTimeout, cancellationToken));
            if (finished == tcs.Task && tcs.Task.Result is { } id)
            {
                _owner.Id = id;
                _logger.LogInformation("Owner {Owner} resolved to {Id}", owner.FullName, id);
                return "";
            }
        }
        finally
        {
            client.Avatars.AvatarPickerReply -= OnReply;
        }

        _logger.LogWarning("Could not resolve owner {Owner}", owner.FullName);
        return $" Warning: could not find the owner avatar '{owner.DisplayName}'. Check the Owner setting, or set it to the owner's UUID.";
    }

    private void OnDisconnected(object? sender, DisconnectedEventArgs e)
    {
        _logger.LogWarning("Disconnected: {Reason} {Message}", e.Reason, e.Message);
        _lastError = $"Disconnected ({e.Reason}){(string.IsNullOrWhiteSpace(e.Message) ? "" : ": " + e.Message)}";
        _state = ConnectionState.LoggedOut;
        _activity.Write("disconnected", _lastError);
    }

    private SessionResult Fail(string message)
    {
        _lastError = message;
        _logger.LogWarning("{Message}", message);
        return new(false, message);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            try { await LogoutAsync(CancellationToken.None); }
            catch (Exception e) { _logger.LogWarning(e, "Error logging out during shutdown"); }
        }
        _gate.Dispose();
    }
}
