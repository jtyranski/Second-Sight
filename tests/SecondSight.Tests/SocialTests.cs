using LibreMetaverse;
using SecondSight.Core;
using SecondSight.Core.Chat;
using SecondSight.Core.Session;
using SecondSight.Core.Social;
using SecondSight.Tools;

namespace SecondSight.Tests;

public class FriendRequestTests
{
    private static readonly UUID Jim = UUID.Random();
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 15, 0, 0, TimeSpan.FromHours(-6));

    [Fact]
    public void Owners_requests_are_accepted_and_others_go_to_the_owner()
    {
        var owner = new OwnerIdentity(AvatarName.Parse("Jimmi Tiponi")) { Id = Jim };
        Assert.Equal(FriendRequestAction.Accept, BotSession.DecideFriendRequest(Jim, "Jimmi Tiponi", owner));
        Assert.Equal(FriendRequestAction.AskOwner, BotSession.DecideFriendRequest(UUID.Random(), "Some One", owner));
        Assert.Equal(FriendRequestAction.AskOwner, BotSession.DecideFriendRequest(UUID.Random(), "Jimmi Tiponi", owner));
    }

    [Fact]
    public void Book_holds_one_request_per_person_and_takes_by_name_or_id()
    {
        var book = new FriendRequestBook();
        var someone = new FriendRequest(UUID.Random(), "Some One", UUID.Random(), Now);
        var bob = new FriendRequest(UUID.Random(), "bobsmith Resident", UUID.Random(), Now);
        Assert.True(book.Add(someone));
        Assert.False(book.Add(someone with { SessionId = UUID.Random() }));
        Assert.True(book.Add(bob));
        Assert.Equal(2, book.Pending.Count);

        Assert.Same(bob, book.Take("BobSmith").Found);
        Assert.Equal(someone.AgentId, book.Take(someone.AgentId.ToString()).Found!.AgentId);
        Assert.Empty(book.Pending);
        Assert.Null(book.Take("Some One").Found);
    }

    [Fact]
    public void Book_remembers_whether_a_request_arrived_offline()
    {
        var book = new FriendRequestBook();
        book.Add(new FriendRequest(UUID.Random(), "Alt Person", UUID.Random(), Now, Offline: true));
        Assert.True(book.Take("Alt Person").Found!.Offline);
    }

    [Fact]
    public void Book_keeps_at_most_fifty()
    {
        var book = new FriendRequestBook();
        for (var i = 0; i < 60; i++) book.Add(new FriendRequest(UUID.Random(), $"Person{i} Resident", UUID.Random(), Now));
        Assert.Equal(FriendRequestBook.MaxPending, book.Pending.Count);
        Assert.Equal("Person10 Resident", book.Pending[0].Name);
    }
}

public class PersonMatcherTests
{
    private sealed record P(UUID Id, string? Name);

    private static readonly P Jim = new(UUID.Random(), "Jimmi Tiponi");
    private static readonly P Jimbo = new(UUID.Random(), "Jimmi Other");
    private static readonly P Dex = new(UUID.Random(), "DexterFrost Resident");
    private static readonly P Unknown = new(UUID.Random(), null);
    private static readonly List<P> People = [Jim, Jimbo, Dex, Unknown];

    private static PersonMatch<P> Find(string who) => PersonMatcher.Find(People, who, p => p.Id, p => p.Name);

    [Fact]
    public void Matches_full_names_usernames_and_uuids()
    {
        Assert.Same(Jim, Find("jimmi tiponi").Found);
        Assert.Same(Jim, Find("jimmi.tiponi").Found);
        Assert.Same(Dex, Find("DexterFrost").Found);
        Assert.Same(Unknown, Find(Unknown.Id.ToString()).Found);
    }

    [Fact]
    public void A_shared_first_name_is_ambiguous()
    {
        var match = Find("Jimmi");
        Assert.Null(match.Found);
        Assert.True(match.Ambiguous);
        Assert.Equal(2, match.Candidates.Count);
    }

    [Fact]
    public void No_match_is_empty()
    {
        Assert.Null(Find("Nobody Here").Found);
        Assert.False(Find("Nobody Here").Ambiguous);
        Assert.Null(Find("  ").Found);
    }
}

public class FriendsFormatterTests
{
    [Fact]
    public void Lists_owner_first_with_status_and_pending_requests()
    {
        var jim = UUID.Random();
        var friends = new List<Friend>
        {
            new(UUID.Random(), "Zed Zebra", false, true),
            new(UUID.Random(), "Amy Resident", true, true),
            new(jim, "Jimmi Tiponi", true, true),
            new(UUID.Random(), "Hidden Person", false, false),
        };
        var pending = new List<FriendRequest> { new(UUID.Random(), "Some One", UUID.Random(), new DateTimeOffset(2026, 11, 22, 15, 4, 0, TimeSpan.Zero)) };
        var text = FriendsFormatter.List(friends, jim, pending).Replace("\r", "");

        Assert.Contains("The bot has 4 friends (2 online):\n- Jimmi Tiponi (online), your owner\n- Amy (online)\n- Hidden Person (online status hidden)\n- Zed Zebra (offline)", text);
        Assert.Contains("Friend requests waiting for your owner's decision (1):\n- Some One (received Nov 22, 3:04 PM)", text);
    }

    [Fact]
    public void Empty_lists()
    {
        var text = FriendsFormatter.List([], null, []);
        Assert.Contains("The bot has no friends on its list.", text);
        Assert.Contains("No friend requests are waiting.", text);
    }

    [Theory]
    [InlineData("Home position set.", true)]
    [InlineData("You can only set your 'Home Location' on your land or at a mainland Infohub.", false)]
    public void Recognizes_set_home_replies(string reply, bool ok) => Assert.Equal(ok, BotSession.SetHomeSucceeded(reply));
}

public class SocialToolsTests
{
    private sealed class FakeSocial : ISocial
    {
        public readonly List<string> Calls = [];

        public Task<SessionResult> SetHomeAsync(CancellationToken c) { Calls.Add("home"); return Ok("home set"); }
        public SessionResult ListFriends() { Calls.Add("list"); return new(true, "friends"); }
        public Task<SessionResult> RespondToFriendRequestAsync(string who, bool accept, CancellationToken c) { Calls.Add($"respond {who} {accept}"); return Ok("done"); }
        public Task<SessionResult> OfferFriendshipToOwnerAsync(CancellationToken c) { Calls.Add("offer"); return Ok("sent"); }
        public Task<SessionResult> RemoveFriendAsync(string who, CancellationToken c) { Calls.Add($"remove {who}"); return Ok("removed"); }
        private static Task<SessionResult> Ok(string m) => Task.FromResult(new SessionResult(true, m));
    }

    [Fact]
    public async Task Tools_pass_through()
    {
        var fake = new FakeSocial();
        var tools = new SocialTools(fake);
        Assert.Equal("home set", await tools.SetHome());
        Assert.Equal("friends", tools.ListFriends());
        Assert.Equal("done", await tools.RespondFriendRequest("Some One", false));
        Assert.Equal("sent", await tools.SendFriendRequestToOwner());
        Assert.Equal("removed", await tools.RemoveFriend("Amy"));
        Assert.Equal(["home", "list", "respond Some One False", "offer", "remove Amy"], fake.Calls);
    }
}
