namespace SecondSight.Core.Session;

public sealed record SessionResult(bool Success, string Message);

/// <summary>The bot's connection to Second Life. One instance per process.</summary>
public interface IBotSession
{
    ConnectionState State { get; }
    Task<SessionResult> LoginAsync(CancellationToken cancellationToken);
    Task<SessionResult> LogoutAsync(CancellationToken cancellationToken);
    BotStatus GetStatus();
}
