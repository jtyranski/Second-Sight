namespace SecondSight.Core;

/// <summary>
/// A Second Life avatar name split into the first/last form the login server expects.
/// Accepts "First Last", "first.last", or a single-word username (which gets the
/// implicit "Resident" last name).
/// </summary>
public sealed record AvatarName(string First, string Last)
{
    public const string ImplicitLastName = "Resident";

    public static AvatarName Parse(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Avatar name is empty.", nameof(name));

        var parts = name.Trim().Split([' ', '.'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            1 => new AvatarName(parts[0], ImplicitLastName),
            2 => new AvatarName(parts[0], parts[1]),
            _ => throw new ArgumentException($"'{name}' is not a valid avatar name. Use 'First Last' or a single username.", nameof(name)),
        };
    }

    public static bool TryParse(string? name, out AvatarName result)
    {
        result = null!;
        if (string.IsNullOrWhiteSpace(name)) return false;
        try { result = Parse(name); return true; }
        catch (ArgumentException) { return false; }
    }

    /// <summary>Full name as Second Life shows it, e.g. "Jimmi Tiponi" or "DexterFrost Resident".</summary>
    public string FullName => $"{First} {Last}";

    /// <summary>Name as people usually write it: the "Resident" last name is dropped.</summary>
    public string DisplayName => Last.Equals(ImplicitLastName, StringComparison.OrdinalIgnoreCase) ? First : FullName;

    /// <summary>True when <paramref name="other"/> names the same avatar, ignoring case and the implicit "Resident".</summary>
    public bool Matches(string other)
    {
        if (string.IsNullOrWhiteSpace(other)) return false;
        AvatarName parsed;
        try { parsed = Parse(other); }
        catch (ArgumentException) { return false; }
        return First.Equals(parsed.First, StringComparison.OrdinalIgnoreCase)
            && Last.Equals(parsed.Last, StringComparison.OrdinalIgnoreCase);
    }

    public override string ToString() => DisplayName;
}
