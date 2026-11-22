using LibreMetaverse;

namespace SecondSight.Core.Perception;

/// <summary>An avatar the bot can see (or knows is in the region from the minimap).</summary>
public sealed record SeenAvatar
{
    public required UUID Id { get; init; }
    public string? Name { get; init; }
    public required Vector3 Position { get; init; }
    public bool Sitting { get; init; }

    /// <summary>True when only the minimap ("coarse") position is known: the avatar is farther than draw distance.</summary>
    public bool Approximate { get; init; }
}

public enum SceneObjectKind { Object, Tree }

/// <summary>A whole object (linkset) near the bot.</summary>
public sealed record SeenObject
{
    public required UUID Id { get; init; }
    public uint LocalId { get; init; }
    public SceneObjectKind Kind { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public required Vector3 Position { get; init; }
    public Vector3 Scale { get; init; }
    public int PrimCount { get; init; } = 1;
    public ClickAction ClickAction { get; init; }
    public bool Touchable { get; init; }
    public bool Scripted { get; init; }
    public string? HoverText { get; init; }
    public bool HasSitter { get; init; }
}

/// <summary>Everything describe_surroundings reports, gathered from the simulator at one moment.</summary>
public sealed record Scene
{
    public required string Region { get; init; }
    public string? ParcelName { get; init; }
    public string? ParcelDescription { get; init; }
    public required Vector3 SelfPosition { get; init; }
    public string? SittingOn { get; init; }
    public bool Flying { get; init; }
    public required string OwnerName { get; init; }

    /// <summary>The owner, or null when they are not in this region.</summary>
    public SeenAvatar? Owner { get; init; }

    public IReadOnlyList<SeenAvatar> Avatars { get; init; } = [];
    public IReadOnlyList<SeenObject> Objects { get; init; } = [];
    public float Radius { get; init; }

    /// <summary>How many objects were in range before the list was cut to the nearest ones.</summary>
    public int ObjectsInRange { get; init; }
}
