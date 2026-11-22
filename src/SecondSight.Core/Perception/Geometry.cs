using LibreMetaverse;

namespace SecondSight.Core.Perception;

/// <summary>Distances and compass directions inside a region. +X is east and +Y is north.</summary>
public static class Geometry
{
    private static readonly string[] Points = ["east", "northeast", "north", "northwest", "west", "southwest", "south", "southeast"];

    public static float Distance(Vector3 a, Vector3 b) => Vector3.Distance(a, b);

    public static float FlatDistance(Vector3 a, Vector3 b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>The compass direction from <paramref name="from"/> to <paramref name="to"/>, or "here" when they are about the same spot.</summary>
    public static string Compass(Vector3 from, Vector3 to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        if (dx * dx + dy * dy < 0.25f) return "here";
        var angle = MathF.Atan2(dy, dx) * 180f / MathF.PI; // 0 = east, 90 = north
        var index = (int)MathF.Round(((angle % 360f) + 360f) % 360f / 45f) % 8;
        return Points[index];
    }

    /// <summary>"12 m northeast", plus how far above or below when the height difference is noticeable.</summary>
    public static string Describe(Vector3 from, Vector3 to)
    {
        var dz = to.Z - from.Z;
        if (FlatDistance(from, to) < 0.5f)
            return MathF.Abs(dz) < 1 ? "right here" : $"{MathF.Abs(dz):0} m straight {(dz > 0 ? "above" : "below")}";
        var text = $"{Distance(from, to):0} m {Compass(from, to)}";
        if (MathF.Abs(dz) >= 3) text += dz > 0 ? $", {dz:0} m above" : $", {-dz:0} m below";
        return text;
    }

    /// <summary>The position in region coordinates of a prim that may be linked to (or seated on) a parent.</summary>
    public static Vector3 WorldPosition(Primitive prim, Func<uint, Primitive?> lookup)
    {
        var position = prim.Position;
        var parentId = prim.ParentID;
        for (var depth = 0; parentId != 0 && depth < 4; depth++)
        {
            var parent = lookup(parentId);
            if (parent == null) break;
            position = parent.Position + position * parent.Rotation;
            parentId = parent.ParentID;
        }
        return position;
    }
}
