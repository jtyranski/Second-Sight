using LibreMetaverse;

namespace SecondSight.Core.Chat;

/// <summary>Who the owner is. Matched by UUID once known; by avatar name only until then.</summary>
public sealed class OwnerIdentity(AvatarName? name)
{
    private UUID? _id;

    public AvatarName? Name { get; } = name;

    public UUID? Id
    {
        get => _id;
        set => _id = value;
    }

    public static OwnerIdentity FromConfig(string? owner)
    {
        if (string.IsNullOrWhiteSpace(owner)) return new OwnerIdentity(null);
        if (UUID.TryParse(owner, out var id)) return new OwnerIdentity(null) { Id = id };
        try { return new OwnerIdentity(AvatarName.Parse(owner)); }
        catch (ArgumentException) { return new OwnerIdentity(null); }
    }

    /// <summary>
    /// True when this avatar is the owner. Objects can claim any name, so only avatars
    /// are matched, and a name match is used only while the UUID is still unknown.
    /// </summary>
    public bool IsOwner(UUID id, string? name, bool isAvatar)
    {
        if (!isAvatar) return false;
        if (_id is { } known) return id == known;
        return Name != null && name != null && Name.Matches(name);
    }
}
