using System.Text.Json;

namespace SecondSight.Core.Config;

public static class ConfigLoader
{
    public const string ConfigPathVariable = "SECONDSIGHT_CONFIG";

    /// <summary>Default config location: %APPDATA%\SecondSight\config.json on Windows, ~/.config/SecondSight/config.json elsewhere.</summary>
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SecondSight", "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The config file path used by the last <see cref="Load"/>, and whether it existed.</summary>
    public static string? ResolvedPath { get; private set; }

    /// <summary>Loads the config file (if present) and applies environment variable overrides.</summary>
    public static BotConfig Load(Func<string, string?>? getEnv = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;
        var path = getEnv(ConfigPathVariable);
        if (string.IsNullOrWhiteSpace(path)) path = DefaultPath;
        ResolvedPath = File.Exists(path) ? path : path + " (not found)";

        var config = File.Exists(path)
            ? JsonSerializer.Deserialize<BotConfig>(File.ReadAllText(path), JsonOptions) ?? new BotConfig()
            : new BotConfig();

        ApplyEnvironment(config, getEnv);
        return config;
    }

    public static BotConfig Parse(string json, Func<string, string?>? getEnv = null)
    {
        var config = JsonSerializer.Deserialize<BotConfig>(json, JsonOptions) ?? new BotConfig();
        if (getEnv != null) ApplyEnvironment(config, getEnv);
        return config;
    }

    private static void ApplyEnvironment(BotConfig config, Func<string, string?> getEnv)
    {
        if (getEnv("SECONDSIGHT_USERNAME") is { Length: > 0 } user) config.Username = user;
        if (getEnv("SECONDSIGHT_PASSWORD") is { Length: > 0 } pass) config.Password = pass;
        if (getEnv("SECONDSIGHT_OWNER") is { Length: > 0 } owner) config.Owner = owner;
        if (getEnv("SECONDSIGHT_START") is { Length: > 0 } start) config.StartLocation = start;
        if (getEnv("SECONDSIGHT_LOGIN_URI") is { Length: > 0 } uri) config.LoginUri = uri;
        if (getEnv("SECONDSIGHT_AUTOLOGIN") is { Length: > 0 } auto && bool.TryParse(auto, out var b)) config.AutoLogin = b;
    }
}
