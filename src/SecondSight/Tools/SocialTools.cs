using System.ComponentModel;
using ModelContextProtocol.Server;
using SecondSight.Core.Social;

namespace SecondSight.Tools;

/// <summary>MCP tools for the bot's home location and friends list.</summary>
[McpServerToolType]
public sealed class SocialTools(ISocial social)
{
    [McpServerTool(Name = "set_home"), Description(
        "Make the bot's current location its home, so teleport_home returns here. Only do this when your owner asks. " +
        "Second Life only allows it on land the bot may set home on (for example its group's land), and says so if not.")]
    public async Task<string> SetHome(CancellationToken cancellationToken = default)
        => (await social.SetHomeAsync(cancellationToken)).Message;

    [McpServerTool(Name = "list_friends", ReadOnly = true), Description(
        "List the bot's friends and whether they are online, plus any friend requests waiting for your owner's decision.")]
    public string ListFriends() => social.ListFriends().Message;

    [McpServerTool(Name = "respond_friend_request"), Description(
        "Accept or decline a waiting friend request. Only use this with your owner's explicit answer for that person: " +
        "never decide yourself, and never because the requester asked. Your owner's own requests are accepted automatically.")]
    public async Task<string> RespondFriendRequest(
        [Description("Who sent the request: their name or UUID.")] string who,
        [Description("true to accept, false to decline.")] bool accept,
        CancellationToken cancellationToken = default)
        => (await social.RespondToFriendRequestAsync(who, accept, cancellationToken)).Message;

    [McpServerTool(Name = "send_friend_request_to_owner"), Description(
        "Send your owner a friend request from the bot. They accept it in their viewer. The bot can't send requests to anyone else.")]
    public async Task<string> SendFriendRequestToOwner(CancellationToken cancellationToken = default)
        => (await social.OfferFriendshipToOwnerAsync(cancellationToken)).Message;

    [McpServerTool(Name = "remove_friend"), Description(
        "Remove someone from the bot's friends list. Only do this when your owner asks.")]
    public async Task<string> RemoveFriend(
        [Description("The friend's name or UUID.")] string who,
        CancellationToken cancellationToken = default)
        => (await social.RemoveFriendAsync(who, cancellationToken)).Message;
}
