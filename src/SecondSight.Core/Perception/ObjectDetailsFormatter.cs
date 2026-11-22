using System.Text;
using LibreMetaverse;

namespace SecondSight.Core.Perception;

/// <summary>Writes the inspect_object report.</summary>
public static class ObjectDetailsFormatter
{
    public sealed record Details
    {
        public required SeenObject Object { get; init; }
        public Primitive.ObjectProperties? Properties { get; init; }
        public string? OwnerName { get; init; }
        public string? CreatorName { get; init; }

        /// <summary>Set when the id named one part of a bigger object: the whole object's name.</summary>
        public string? PartOf { get; init; }

        /// <summary>Set when the object is worn: who wears it.</summary>
        public string? WornBy { get; init; }

        public string? SitText { get; init; }
        public bool Physical { get; init; }
        public bool Phantom { get; init; }
        public bool Temporary { get; init; }
    }

    public static string Describe(Details d, Vector3 me)
    {
        var o = d.Object;
        var p = d.Properties;
        var sb = new StringBuilder();
        sb.AppendLine($"{o.Name ?? (o.Kind == SceneObjectKind.Tree ? "Tree" : "(unnamed object)")} (id {o.Id}, local id {o.LocalId})");
        if (d.PartOf != null) sb.AppendLine($"This is one part of \"{d.PartOf}\".");
        if (d.WornBy != null) sb.AppendLine($"Worn by {d.WornBy}.");
        if (o.Description != null) sb.AppendLine($"Description: \"{SceneFormatter.Shorten(o.Description, 500)}\"");
        if (o.HoverText != null) sb.AppendLine($"Floating text: \"{SceneFormatter.Shorten(o.HoverText, 500)}\"");
        if (d.WornBy == null)
            sb.AppendLine($"Where: {Geometry.Describe(me, o.Position)}, at ({o.Position.X:0.#}, {o.Position.Y:0.#}, {o.Position.Z:0.#}).");
        sb.AppendLine($"Size: {o.Scale.X:0.##} x {o.Scale.Y:0.##} x {o.Scale.Z:0.##} m{(o.PrimCount > 1 ? $", {o.PrimCount} linked parts" : "")}.");
        if (p != null)
        {
            if (p.OwnerID != UUID.Zero) sb.AppendLine($"Owner: {d.OwnerName ?? (p.GroupID != UUID.Zero && p.OwnerID == p.GroupID ? "a group" : p.OwnerID.ToString())}");
            if (p.CreatorID != UUID.Zero) sb.AppendLine($"Creator: {d.CreatorName ?? p.CreatorID.ToString()}");
            if (p.SaleType != SaleType.Not && p.SalePrice >= 0)
                sb.AppendLine($"For sale: L${p.SalePrice} ({p.SaleType.ToString().ToLowerInvariant()}).");
        }
        else sb.AppendLine("Owner and creator: not loaded (the region didn't answer in time).");

        var actions = SceneFormatter.Actions(o);
        if (d.SitText != null) actions.Add($"sit menu says \"{SceneFormatter.Shorten(d.SitText, 60)}\"");
        if (o.HasSitter) actions.Add("someone is sitting on it");
        if (o.Scripted) actions.Add("scripted");
        if (d.Physical) actions.Add("physical (can move)");
        if (d.Phantom) actions.Add("phantom (can walk through)");
        if (d.Temporary) actions.Add("temporary");
        if (actions.Count > 0) sb.AppendLine("Notes: " + string.Join(", ", actions) + ".");
        sb.Append(SceneFormatter.UntrustedNote);
        return sb.ToString();
    }

    public static string DescribeAvatar(SeenAvatar a, Vector3 me, bool isOwner, string? sittingOn)
    {
        var sb = new StringBuilder();
        sb.Append($"{a.Name ?? "(name not loaded yet)"} (avatar, id {a.Id})");
        if (isOwner) sb.Append(", your owner");
        sb.AppendLine(".");
        sb.Append($"Where: {SceneFormatter.Where(me, a)}");
        if (a.Sitting) sb.Append(sittingOn != null ? $", sitting on {sittingOn}" : ", sitting");
        sb.Append('.');
        return sb.ToString();
    }
}
