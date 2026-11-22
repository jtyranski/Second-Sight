using LibreMetaverse;
using SecondSight.Core;
using SecondSight.Core.Chat;
using SecondSight.Core.Session;
using SecondSight.Core.Travel;
using SecondSight.Tools;

namespace SecondSight.Tests;

public class LureDecisionTests
{
    private static readonly UUID Self = UUID.Random();
    private static readonly UUID Jim = UUID.Random();
    private static readonly UUID Stranger = UUID.Random();
    private static readonly OwnerIdentity Owner = new(AvatarName.Parse("Jimmi Tiponi")) { Id = Jim };

    private static LureAction Decide(UUID from, string name, InstantMessageDialog dialog) =>
        LureDecision.Decide(new InstantMessage { FromAgentID = from, FromAgentName = name, Dialog = dialog }, Self, Owner);

    [Fact]
    public void Accepts_only_the_owners_teleport_offers()
    {
        Assert.Equal(LureAction.AcceptOffer, Decide(Jim, "Jimmi Tiponi", InstantMessageDialog.RequestTeleport));
        Assert.Equal(LureAction.DeclineOffer, Decide(Stranger, "Some One", InstantMessageDialog.RequestTeleport));
    }

    [Fact]
    public void Stranger_using_owner_name_is_declined_once_uuid_is_known() =>
        Assert.Equal(LureAction.DeclineOffer, Decide(Stranger, "Jimmi Tiponi", InstantMessageDialog.RequestTeleport));

    [Fact]
    public void Sends_offers_only_when_the_owner_asks()
    {
        Assert.Equal(LureAction.SendOfferToOwner, Decide(Jim, "Jimmi Tiponi", InstantMessageDialog.RequestLure));
        Assert.Equal(LureAction.IgnoreRequest, Decide(Stranger, "Some One", InstantMessageDialog.RequestLure));
    }

    [Fact]
    public void Other_ims_are_not_lures()
    {
        Assert.Equal(LureAction.None, Decide(Jim, "Jimmi Tiponi", InstantMessageDialog.MessageFromAgent));
        Assert.Equal(LureAction.None, Decide(Self, "DexterFrost Resident", InstantMessageDialog.RequestTeleport));
    }
}

public class TeleportTargetTests
{
    [Theory]
    [InlineData("Ahern", "Ahern", 128, 128, 30)]
    [InlineData("Ahern/10/20/30", "Ahern", 10, 20, 30)]
    [InlineData("Ahern/10/20", "Ahern", 10, 20, 30)]
    [InlineData("http://maps.secondlife.com/secondlife/Da%20Boom/50/60/70", "Da Boom", 50, 60, 70)]
    [InlineData("https://maps.secondlife.com/secondlife/Sandbox+Island/1/2/3/", "Sandbox Island", 1, 2, 3)]
    [InlineData("secondlife:///app/teleport/Ahern/5.5/6/7", "Ahern", 5.5f, 6, 7)]
    public void Parses_slurls_and_region_paths(string text, string region, float x, float y, float z)
    {
        Assert.True(TeleportTarget.TryParse(text, out var target, out var error), error);
        Assert.Equal(new TeleportTarget(region, x, y, z), target);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ahern/x/2/3")]
    [InlineData("Ahern/300/2/3")]
    [InlineData("Ahern/1/2/3/4")]
    [InlineData("/1/2/3")]
    public void Rejects_bad_locations(string text) =>
        Assert.False(TeleportTarget.TryParse(text, out _, out _));

    [Fact]
    public void Validates_coordinates()
    {
        Assert.True(TeleportTarget.TryCreate("Ahern", 0, 255.9f, 4096, out _, out _));
        Assert.False(TeleportTarget.TryCreate("Ahern", -1, 10, 10, out _, out _));
        Assert.False(TeleportTarget.TryCreate("Ahern", 10, 256, 10, out _, out _));
        Assert.False(TeleportTarget.TryCreate("Ahern", 10, 10, 5000, out _, out _));
        Assert.False(TeleportTarget.TryCreate("  ", 10, 10, 10, out _, out _));
    }
}

public class TeleportToolsTests
{
    private sealed class FakeTravel : ITravel
    {
        public TeleportTarget? Last;
        public int Homes, Offers;
        public string? OfferMessage;

        public Task<SessionResult> TeleportAsync(TeleportTarget target, CancellationToken cancellationToken)
        {
            Last = target;
            return Task.FromResult(new SessionResult(true, $"Teleported to {target}."));
        }

        public Task<SessionResult> GoHomeAsync(CancellationToken cancellationToken)
        {
            Homes++;
            return Task.FromResult(new SessionResult(true, "Teleported home."));
        }

        public Task<SessionResult> OfferTeleportToOwnerAsync(string? message, CancellationToken cancellationToken)
        {
            Offers++;
            OfferMessage = message;
            return Task.FromResult(new SessionResult(true, "Sent."));
        }
    }

    [Fact]
    public async Task Teleport_with_slurl()
    {
        var travel = new FakeTravel();
        await new TeleportTools(travel).Teleport("http://maps.secondlife.com/secondlife/Ahern/10/20/30");
        Assert.Equal(new TeleportTarget("Ahern", 10, 20, 30), travel.Last);
    }

    [Fact]
    public async Task Teleport_with_region_and_partial_coordinates()
    {
        var travel = new FakeTravel();
        await new TeleportTools(travel).Teleport("Sandbox Island", x: 40);
        Assert.Equal(new TeleportTarget("Sandbox Island", 40, 128, 30), travel.Last);
    }

    [Fact]
    public async Task Teleport_rejects_bad_input_without_moving()
    {
        var travel = new FakeTravel();
        var result = await new TeleportTools(travel).Teleport("Ahern", x: 999);
        Assert.Contains("between 0 and 255", result);
        Assert.Null(travel.Last);
    }

    [Fact]
    public async Task Home_and_offer_delegate()
    {
        var travel = new FakeTravel();
        var tools = new TeleportTools(travel);
        await tools.TeleportHome();
        await tools.OfferTeleport("come see");
        Assert.Equal((1, 1, "come see"), (travel.Homes, travel.Offers, travel.OfferMessage));
    }

    [Fact]
    public void Events_are_labeled_in_message_list()
    {
        var line = MessageFormatter.Line(new ChatMessage
        {
            Time = DateTimeOffset.UnixEpoch, Channel = MessageChannel.Event, FromName = "Second Sight", FromId = "",
            Speaker = SpeakerKind.System, Text = "Teleport finished.", FromOwner = true,
        });
        Assert.Contains("Second Sight (event, system): Teleport finished.", line);
    }
}
