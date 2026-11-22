using SecondSight.Core.Session;

namespace SecondSight.Tests;

public class BotStatusTests
{
    [Fact]
    public void Describes_logged_out_with_owner()
    {
        var text = new BotStatus { State = ConnectionState.LoggedOut, OwnerName = "Jimmi Tiponi" }.Describe();
        Assert.Equal("Logged out. Owner: Jimmi Tiponi (UUID not resolved yet).", text);
    }

    [Fact]
    public void Describes_logged_in_position_and_posture()
    {
        var status = new BotStatus
        {
            State = ConnectionState.LoggedIn,
            AvatarName = "DexterFrost Resident",
            Region = "Ahern",
            Position = (128.4f, 64.6f, 22.1f),
            Flying = true,
            OwnerName = "Jimmi Tiponi",
            OwnerId = "11111111-2222-3333-4444-555555555555",
        };
        Assert.Equal(
            "Logged in as DexterFrost Resident in region Ahern at (128, 65, 22). Flying. " +
            "Owner: Jimmi Tiponi (11111111-2222-3333-4444-555555555555).",
            status.Describe());
    }

    [Fact]
    public void Sitting_wins_over_flying()
    {
        var text = new BotStatus { State = ConnectionState.LoggedIn, Sitting = true, Flying = true }.Describe();
        Assert.Contains("Sitting.", text);
        Assert.DoesNotContain("Flying.", text);
    }

    [Fact]
    public void Includes_last_error()
    {
        var text = new BotStatus { State = ConnectionState.LoggedOut, LastError = "Login failed: bad password" }.Describe();
        Assert.EndsWith("Last problem: Login failed: bad password", text);
    }
}
