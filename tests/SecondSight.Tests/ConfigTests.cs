using SecondSight.Core.Config;

namespace SecondSight.Tests;

public class ConfigTests
{
    private static Func<string, string?> Env(Dictionary<string, string> values) =>
        key => values.TryGetValue(key, out var v) ? v : null;

    [Fact]
    public void Parse_reads_json_with_comments_and_any_case()
    {
        var config = ConfigLoader.Parse("""
            {
              // the bot account
              "username": "DexterFrost",
              "Owner": "Jimmi Tiponi",
              "StartLocation": "home",
              "AutoLogin": true,
            }
            """);
        Assert.Equal("DexterFrost", config.Username);
        Assert.Equal("Jimmi Tiponi", config.Owner);
        Assert.Equal("home", config.StartLocation);
        Assert.True(config.AutoLogin);
        Assert.Null(config.Password);
    }

    [Fact]
    public void Environment_overrides_file_values()
    {
        var config = ConfigLoader.Parse("""{ "Username": "FromFile", "Owner": "File Owner" }""",
            Env(new() { ["SECONDSIGHT_USERNAME"] = "FromEnv", ["SECONDSIGHT_PASSWORD"] = "secret", ["SECONDSIGHT_AUTOLOGIN"] = "true" }));
        Assert.Equal("FromEnv", config.Username);
        Assert.Equal("secret", config.Password);
        Assert.Equal("File Owner", config.Owner);
        Assert.True(config.AutoLogin);
    }

    [Fact]
    public void Load_uses_SECONDSIGHT_CONFIG_path()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """{ "Username": "DexterFrost" }""");
            var config = ConfigLoader.Load(Env(new() { [ConfigLoader.ConfigPathVariable] = path }));
            Assert.Equal("DexterFrost", config.Username);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_without_file_gives_empty_config()
    {
        var config = ConfigLoader.Load(Env(new() { [ConfigLoader.ConfigPathVariable] = "/no/such/config.json" }));
        Assert.Null(config.Username);
        Assert.Equal("last", config.StartLocation);
    }

    [Fact]
    public void Validate_lists_missing_settings()
    {
        var problems = new BotConfig().Validate();
        Assert.Equal(3, problems.Count);
    }

    [Fact]
    public void Validate_passes_complete_config_and_never_echoes_password()
    {
        var config = new BotConfig { Username = "DexterFrost", Password = "hunter2", Owner = "Jimmi Tiponi" };
        Assert.Empty(config.Validate());

        config.StartLocation = "/bad";
        var problems = config.Validate();
        Assert.Single(problems);
        Assert.DoesNotContain(problems, p => p.Contains("hunter2"));
    }
}
