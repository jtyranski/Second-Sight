using LibreMetaverse;

namespace SecondSight.Core.Perception;

/// <summary>Turns the simulator's raw object and avatar tables into the objects and avatars near the bot.</summary>
public static class SceneBuilder
{
    /// <summary>
    /// Whole objects (root prims) within <paramref name="radius"/> of <paramref name="center"/>, nearest first.
    /// Attachments, grass, and particle systems are left out.
    /// </summary>
    public static List<SeenObject> NearbyObjects(IReadOnlyCollection<Primitive> prims, Vector3 center, float radius,
        IReadOnlySet<uint>? seatedOn = null, Func<UUID, Primitive.ObjectProperties?>? properties = null)
    {
        var childCounts = new Dictionary<uint, int>();
        foreach (var p in prims)
            if (p.ParentID != 0) childCounts[p.ParentID] = childCounts.GetValueOrDefault(p.ParentID) + 1;

        var result = new List<SeenObject>();
        foreach (var p in prims)
        {
            if (p.ParentID != 0 || p.IsAttachment) continue;
            var kind = KindOf(p);
            if (kind == null) continue;
            if (Geometry.Distance(center, p.Position) > radius) continue;
            result.Add(ToSeen(p, kind.Value, childCounts.GetValueOrDefault(p.LocalID) + 1,
                seatedOn?.Contains(p.LocalID) == true, properties?.Invoke(p.ID) ?? p.Properties));
        }
        result.Sort((a, b) => Geometry.Distance(center, a.Position).CompareTo(Geometry.Distance(center, b.Position)));
        return result;
    }

    public static SeenObject ToSeen(Primitive p, SceneObjectKind kind, int primCount, bool hasSitter, Primitive.ObjectProperties? props) => new()
    {
        Id = p.ID,
        LocalId = p.LocalID,
        Kind = kind,
        Name = Clean(props?.Name),
        Description = Clean(props?.Description),
        Position = p.Position,
        Scale = p.Scale,
        PrimCount = primCount,
        ClickAction = p.ClickAction,
        Touchable = (p.Flags & PrimFlags.Touch) != 0,
        Scripted = (p.Flags & PrimFlags.Scripted) != 0,
        HoverText = Clean(p.Text),
        HasSitter = hasSitter,
    };

    public static SceneObjectKind? KindOf(Primitive p) => p.PrimData.PCode switch
    {
        PCode.Prim => SceneObjectKind.Object,
        PCode.Tree or PCode.NewTree => SceneObjectKind.Tree,
        _ => null,
    };

    /// <summary>
    /// Avatars in the region other than the bot: the ones the bot can see in full, then the ones it only knows
    /// from the minimap. <paramref name="lookup"/> finds a seat so seated avatars get a region position.
    /// </summary>
    public static List<SeenAvatar> Avatars(IEnumerable<Avatar> visible, IReadOnlyDictionary<UUID, Vector3>? coarse,
        UUID self, Func<uint, Primitive?> lookup, Func<UUID, string?>? nameOf = null)
    {
        var result = new List<SeenAvatar>();
        var seen = new HashSet<UUID> { self };
        foreach (var a in visible)
        {
            if (!seen.Add(a.ID)) continue;
            var name = string.IsNullOrWhiteSpace(a.Name) ? nameOf?.Invoke(a.ID) : a.Name;
            result.Add(new SeenAvatar
            {
                Id = a.ID,
                Name = CleanName(name),
                Position = Geometry.WorldPosition(a, lookup),
                Sitting = a.ParentID != 0,
            });
        }
        if (coarse != null)
            foreach (var (id, pos) in coarse)
            {
                if (!seen.Add(id)) continue;
                result.Add(new SeenAvatar { Id = id, Name = CleanName(nameOf?.Invoke(id)), Position = pos, Approximate = true });
            }
        return result;
    }

    private static string? CleanName(string? name)
    {
        name = Clean(name);
        return name?.EndsWith(" Resident", StringComparison.OrdinalIgnoreCase) == true ? name[..^" Resident".Length] : name;
    }

    /// <summary>Trims text and drops control characters; empty text becomes null.</summary>
    public static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var chars = text.Where(c => !char.IsControl(c) || c == '\n').ToArray();
        var cleaned = new string(chars).Trim().Replace("\n", " / ");
        return cleaned.Length == 0 ? null : cleaned;
    }
}
