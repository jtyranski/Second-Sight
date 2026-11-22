using System.Collections.Frozen;
using System.Text;
using LibreMetaverse;

namespace SecondSight.Core.Perception;

/// <summary>Something an avatar is wearing: an attached object and where it is attached.</summary>
public sealed record WornItem(UUID Id, string? Name, AttachmentPoint Point)
{
    /// <summary>Attachments on a hand are what the avatar is holding.</summary>
    public bool Held => Point is AttachmentPoint.LeftHand or AttachmentPoint.RightHand;

    public override string ToString() => $"{(Name != null ? $"\"{Name}\"" : "an unnamed item")} ({AvatarActivity.PointName(Point)})";
}

/// <summary>Turns an avatar's playing animations into words.</summary>
public static class AvatarActivity
{
    private static readonly FrozenDictionary<UUID, string> BuiltIn = Animations.ToDictionary();

    private static readonly Dictionary<string, string> Friendly = new(StringComparer.OrdinalIgnoreCase)
    {
        ["STAND"] = "standing", ["STAND_1"] = "standing", ["STAND_2"] = "standing", ["STAND_3"] = "standing", ["STAND_4"] = "standing",
        ["WALK"] = "walking", ["FEMALE_WALK"] = "walking", ["STRIDE"] = "walking", ["RUN"] = "running",
        ["FLY"] = "flying", ["FLYSLOW"] = "flying slowly", ["HOVER"] = "hovering", ["HOVER_UP"] = "flying up", ["HOVER_DOWN"] = "flying down",
        ["SIT"] = "sitting", ["SIT_FEMALE"] = "sitting", ["SIT_GENERIC"] = "sitting",
        ["SIT_GROUND"] = "sitting on the ground", ["SIT_GROUND_staticRAINED"] = "sitting on the ground",
        ["SIT_TO_STAND"] = "standing up", ["STANDUP"] = "getting up",
        ["TYPE"] = "typing", ["TALK"] = "talking", ["AWAY"] = "away", ["BUSY"] = "busy", ["SLEEP"] = "sleeping",
        ["CROUCH"] = "crouching", ["CROUCHWALK"] = "crouch-walking", ["PRE_JUMP"] = "jumping", ["JUMP"] = "jumping",
        ["LAND"] = "landing", ["MEDIUM_LAND"] = "landing", ["FALLDOWN"] = "falling",
        ["TURNLEFT"] = "turning left", ["TURNRIGHT"] = "turning right",
        ["CUSTOMIZE"] = "editing their appearance", ["CUSTOMIZE_DONE"] = "editing their appearance",
        ["SNAPSHOT"] = "taking a snapshot",
        ["DANCE1"] = "dancing", ["DANCE2"] = "dancing", ["DANCE3"] = "dancing", ["DANCE4"] = "dancing",
        ["DANCE5"] = "dancing", ["DANCE6"] = "dancing", ["DANCE7"] = "dancing", ["DANCE8"] = "dancing",
        ["HELLO"] = "waving hello", ["CLAP"] = "clapping", ["BOW"] = "bowing", ["COURTBOW"] = "bowing",
        ["YES"] = "nodding yes", ["YES_HAPPY"] = "nodding yes", ["NO"] = "shaking their head", ["NO_UNHAPPY"] = "shaking their head",
        ["SHRUG"] = "shrugging", ["SALUTE"] = "saluting", ["BLOW_KISS"] = "blowing a kiss", ["LAUGH_SHORT"] = "laughing",
        ["BELLY_LAUGH"] = "laughing hard", ["CRY"] = "crying", ["POINT_ME"] = "pointing at themselves", ["POINT_YOU"] = "pointing",
        ["DRINK"] = "drinking", ["SMOKE_IDLE"] = "smoking", ["SMOKE_INHALE"] = "smoking", ["YOGA_FLOAT"] = "floating in a yoga pose",
    };

    /// <summary>The words for a built-in animation, or null when it is a custom one.</summary>
    public static string? FriendlyName(UUID animation)
    {
        if (!BuiltIn.TryGetValue(animation, out var name)) return null;
        if (Friendly.TryGetValue(name, out var friendly)) return friendly;
        if (name.StartsWith("EXPRESS_", StringComparison.Ordinal))
            return "making a face (" + name["EXPRESS_".Length..].ToLowerInvariant().Replace('_', ' ') + ")";
        return name.ToLowerInvariant().Replace('_', ' ');
    }

    /// <summary>
    /// What the avatar is doing: built-in animations by name, then custom animations counted by the object that plays them.
    /// </summary>
    public static List<string> Describe(IEnumerable<Animation> animations, UUID avatarId,
        IReadOnlyCollection<WornItem> worn, Func<UUID, string?> objectName)
    {
        var doing = new List<string>();
        var custom = new Dictionary<string, int>();
        foreach (var a in animations.OrderBy(a => a.AnimationSequence))
        {
            if (FriendlyName(a.AnimationID) is { } name)
            {
                if (!doing.Contains(name)) doing.Add(name);
                continue;
            }
            var source = CustomSource(a.AnimationSourceObjectID, avatarId, worn, objectName);
            custom[source] = custom.GetValueOrDefault(source) + 1;
        }
        foreach (var (source, count) in custom)
            doing.Add($"{(count == 1 ? "a custom animation" : $"{count} custom animations")} {source}");
        return doing;
    }

    private static string CustomSource(UUID source, UUID avatarId, IReadOnlyCollection<WornItem> worn, Func<UUID, string?> objectName)
    {
        if (source == UUID.Zero || source == avatarId) return "(from their animation override or a gesture)";
        if (worn.FirstOrDefault(w => w.Id == source) is { } item)
            return $"from their attachment {item}";
        return objectName(source) is { } name ? $"from \"{name}\"" : "from a nearby object";
    }

    /// <summary>"right hand", "left hand ring", "skull".</summary>
    public static string PointName(AttachmentPoint point)
    {
        var text = point.ToString();
        if (text.StartsWith("HUD", StringComparison.Ordinal)) return "HUD";
        var sb = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsUpper(c) && sb.Length > 0) sb.Append(' ');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString().Replace("alt ", "alt. ");
    }
}
