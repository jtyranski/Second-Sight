using LibreMetaverse;
using Microsoft.Extensions.Logging;
using SecondSight.Core.Config;

namespace SecondSight.Core.Session;

/// <summary>Owns the LibreMetaverse <see cref="GridClient"/> and the login/logout lifecycle.</summary>
public sealed class BotSession : IBotSession, IAsyncDisposable
{
    public const string Channel = "SecondSight";
    public static readonly string Version = typeof(BotSession).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";

    private static readonly TimeSpan OwnerLookupTimeout = TimeSpan.FromSeconds(15);

    private readonly BotConfig _config;
    private readonly ILogger<BotSession> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private GridClient? _client;
    private volatile ConnectionState _state = ConnectionState.LoggedOut;
    private UUID? _ownerId;
    private string? _lastError;

    public BotSession(BotConfig config, ILogger<BotSession> logger)
    {
        _config = config;
        _logger = logger;
    }

    public ConnectionState State => _state;

    /// <summary>The owner's UUID once resolved. Later milestones use this to filter chat and IMs.</summary>
    public UUID? OwnerId => _ownerId;

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

            _client = client;
            _state = ConnectionState.LoggedIn;
            _logger.LogInformation("Logged in to {Region}", client.Network.CurrentSim?.Name);

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
            client.Network.Disconnected -= OnDisconnected;
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
        var ownerId = _ownerId?.ToString();

        if (client == null || _state != ConnectionState.LoggedIn)
            return new BotStatus { State = _state, OwnerName = ownerName, OwnerId = ownerId, LastError = _lastError };

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
        };
    }

    private static string DescribeOwner(string owner) =>
        UUID.TryParse(owner, out _) ? owner : AvatarName.Parse(owner).DisplayName;

    /// <summary>Looks up the owner's UUID by name, unless the config already gives a UUID.</summary>
    private async Task<string> ResolveOwnerAsync(GridClient client, CancellationToken cancellationToken)
    {
        if (UUID.TryParse(_config.Owner ?? "", out var direct))
        {
            _ownerId = direct;
            return "";
        }
        if (_ownerId != null) return "";

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
                _ownerId = id;
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
