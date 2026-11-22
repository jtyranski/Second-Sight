using System.Globalization;
using System.Text;

namespace SecondSight.Core.Session;

public enum ConnectionState { LoggedOut, LoggingIn, LoggedIn, LoggingOut }

/// <summary>A snapshot of the bot's state, as returned by the status tool.</summary>
public sealed record BotStatus
{
    public ConnectionState State { get; init; }
    public string? AvatarName { get; init; }
    public string? Region { get; init; }
    public (float X, float Y, float Z)? Position { get; init; }
    public bool Sitting { get; init; }
    public bool Flying { get; init; }
    public string? OwnerName { get; init; }
    public string? OwnerId { get; init; }
    public string? LastError { get; init; }

    /// <summary>Plain-text summary for Claude.</summary>
    public string Describe()
    {
        var sb = new StringBuilder();
        switch (State)
        {
            case ConnectionState.LoggedOut:
                sb.Append("Logged out.");
                break;
            case ConnectionState.LoggingIn:
                sb.Append("Logging in...");
                break;
            case ConnectionState.LoggingOut:
                sb.Append("Logging out...");
                break;
            case ConnectionState.LoggedIn:
                sb.Append(CultureInfo.InvariantCulture, $"Logged in as {AvatarName ?? "unknown"}");
                if (Region != null) sb.Append(CultureInfo.InvariantCulture, $" in region {Region}");
                if (Position is { } p) sb.Append(CultureInfo.InvariantCulture, $" at ({p.X:0}, {p.Y:0}, {p.Z:0})");
                sb.Append('.');
                if (Sitting) sb.Append(" Sitting.");
                else if (Flying) sb.Append(" Flying.");
                else sb.Append(" Standing.");
                break;
        }

        if (OwnerName != null)
        {
            sb.Append(CultureInfo.InvariantCulture, $" Owner: {OwnerName}");
            sb.Append(OwnerId != null ? $" ({OwnerId})." : " (UUID not resolved yet).");
        }

        if (LastError != null) sb.Append(CultureInfo.InvariantCulture, $" Last problem: {LastError}");
        return sb.ToString();
    }
}
