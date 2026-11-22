using SecondSight.Core.Session;
using SecondSight.Tools;

namespace SecondSight.Tests;

public class SessionToolsTests
{
    private sealed class FakeSession : IBotSession
    {
        public int Logins, Logouts;
        public ConnectionState State { get; set; } = ConnectionState.LoggedOut;

        public Task<SessionResult> LoginAsync(CancellationToken cancellationToken)
        {
            Logins++;
            State = ConnectionState.LoggedIn;
            return Task.FromResult(new SessionResult(true, "Logged in as Test Bot."));
        }

        public Task<SessionResult> LogoutAsync(CancellationToken cancellationToken)
        {
            Logouts++;
            State = ConnectionState.LoggedOut;
            return Task.FromResult(new SessionResult(true, "Logged out."));
        }

        public BotStatus GetStatus() => new() { State = State, AvatarName = "Test Bot" };
    }

    [Fact]
    public async Task Tools_delegate_to_session()
    {
        var session = new FakeSession();
        var tools = new SessionTools(session);

        Assert.Equal("Logged out.", tools.Status());
        Assert.Equal("Logged in as Test Bot.", await tools.Login(CancellationToken.None));
        Assert.StartsWith("Logged in as Test Bot", tools.Status());
        Assert.Equal("Logged out.", await tools.Logout(CancellationToken.None));
        Assert.Equal((1, 1), (session.Logins, session.Logouts));
    }
}
