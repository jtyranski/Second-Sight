using LibreMetaverse;

namespace SecondSight.Core.Social;

/// <summary>The result of looking someone up by name or UUID: exactly one, several, or none.</summary>
public sealed record PersonMatch<T>(T? Found, IReadOnlyList<T> Candidates) where T : class
{
    public bool Ambiguous => Found == null && Candidates.Count > 1;
}

/// <summary>Matches "Jimmi Tiponi", "jimmi.tiponi", "Jimmi" (for Jimmi Resident), a first name alone, or a UUID.</summary>
public static class PersonMatcher
{
    public static PersonMatch<T> Find<T>(IEnumerable<T> people, string who, Func<T, UUID> idOf, Func<T, string?> nameOf) where T : class
    {
        var list = people.ToList();
        who = who.Trim();
        if (who.Length == 0) return new(null, []);

        if (UUID.TryParse(who, out var id))
        {
            var byId = list.FirstOrDefault(p => idOf(p) == id);
            return new(byId, byId == null ? [] : [byId]);
        }

        var exact = list.Where(p => nameOf(p) is { } n && AvatarName.TryParse(n, out var name) && name.Matches(who)).ToList();
        if (exact.Count == 1) return new(exact[0], exact);
        if (exact.Count > 1) return new(null, exact);

        // A first name alone, when only one person has it.
        var first = list.Where(p => nameOf(p) is { } n && n.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is { } f
            && f.Equals(who, StringComparison.OrdinalIgnoreCase)).ToList();
        return first.Count == 1 ? new(first[0], first) : new(null, first);
    }
}
