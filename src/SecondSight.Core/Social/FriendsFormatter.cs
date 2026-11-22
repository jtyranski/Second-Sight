using System.Text;
using LibreMetaverse;

namespace SecondSight.Core.Social;

/// <summary>A friend on the bot's list.</summary>
public sealed record Friend(UUID Id, string? Name, bool Online, bool CanSeeOnline);

public static class FriendsFormatter
{
    public static string DisplayName(string? name) =>
        name == null ? "(name not loaded yet)" : AvatarName.TryParse(name, out var n) ? n.DisplayName : name;

    public static string List(IReadOnlyCollection<Friend> friends, UUID? ownerId, IReadOnlyCollection<FriendRequest> pending)
    {
        var sb = new StringBuilder();
        if (friends.Count == 0) sb.AppendLine("The bot has no friends on its list.");
        else
        {
            var online = friends.Count(f => f.Online);
            sb.AppendLine($"The bot has {friends.Count} friend{(friends.Count == 1 ? "" : "s")} ({online} online):");
            foreach (var f in friends.OrderByDescending(f => f.Id == ownerId).ThenByDescending(f => f.Online)
                         .ThenBy(f => f.Name ?? "~", StringComparer.OrdinalIgnoreCase))
            {
                var status = f.Online ? "online" : f.CanSeeOnline ? "offline" : "online status hidden";
                sb.AppendLine($"- {DisplayName(f.Name)} ({status}){(f.Id == ownerId ? ", your owner" : "")}");
            }
        }
        sb.Append(Pending(pending));
        return sb.ToString().TrimEnd();
    }

    public static string Pending(IReadOnlyCollection<FriendRequest> pending)
    {
        if (pending.Count == 0) return "No friend requests are waiting.";
        var sb = new StringBuilder();
        sb.AppendLine($"Friend requests waiting for your owner's decision ({pending.Count}):");
        foreach (var r in pending.OrderBy(r => r.Received))
            sb.AppendLine($"- {DisplayName(r.Name)} (received {r.Received:MMM d, h:mm tt})");
        return sb.ToString();
    }

    /// <summary>"Several people match..." text for an ambiguous name.</summary>
    public static string Ambiguous<T>(string who, PersonMatch<T> match, Func<T, string?> nameOf) where T : class =>
        $"More than one person matches \"{who}\": {string.Join(", ", match.Candidates.Select(c => DisplayName(nameOf(c))))}. Use the full name.";
}
