namespace SecondSight.Core.Config;

/// <summary>Settings for the bot. Loaded from a JSON file outside the repo, with environment variable overrides.</summary>
public sealed class BotConfig
{
    /// <summary>The bot account's username, e.g. "DexterFrost".</summary>
    public string? Username { get; set; }

    /// <summary>The bot account's password. Prefer the SECONDSIGHT_PASSWORD environment variable.</summary>
    public string? Password { get; set; }

    /// <summary>The owner's avatar name (e.g. "Jimmi Tiponi") or UUID. The bot only takes direction from this avatar.</summary>
    public string? Owner { get; set; }

    /// <summary>Where to log in: "last", "home", or "Region Name/x/y/z".</summary>
    public string StartLocation { get; set; } = "last";

    /// <summary>Login server URL. Defaults to the main Second Life grid.</summary>
    public string? LoginUri { get; set; }

    /// <summary>Log in automatically when the MCP server starts.</summary>
    public bool AutoLogin { get; set; }

    /// <summary>Most chat lines and IMs the bot may send per Second Life day (Pacific time). Linden Lab's hard limit is 5,000.</summary>
    public int DailyMessageLimit { get; set; } = 500;

    /// <summary>Most chat lines and IMs the bot may send in any 60 seconds.</summary>
    public int MessagesPerMinute { get; set; } = 10;

    /// <summary>Folder for the activity log. Defaults to %LOCALAPPDATA%\SecondSight\logs.</summary>
    public string? ActivityLogDirectory { get; set; }

    /// <summary>Returns a list of problems with the config. Never includes the password value.</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(Username)) problems.Add("Username is not set (config 'Username' or SECONDSIGHT_USERNAME).");
        else
        {
            try { AvatarName.Parse(Username); }
            catch (ArgumentException e) { problems.Add(e.Message); }
        }
        if (string.IsNullOrEmpty(Password)) problems.Add("Password is not set (SECONDSIGHT_PASSWORD or config 'Password').");
        if (string.IsNullOrWhiteSpace(Owner)) problems.Add("Owner is not set (config 'Owner' or SECONDSIGHT_OWNER).");
        if (!Session.StartLocationParser.TryParse(StartLocation, out _, out var startError)) problems.Add(startError!);
        if (DailyMessageLimit < 1 || DailyMessageLimit > 5000) problems.Add("DailyMessageLimit must be between 1 and 5000.");
        if (MessagesPerMinute < 1) problems.Add("MessagesPerMinute must be at least 1.");
        return problems;
    }
}
