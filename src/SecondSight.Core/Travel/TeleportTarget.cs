using System.Globalization;

namespace SecondSight.Core.Travel;

/// <summary>A validated teleport destination: region name plus a position inside it.</summary>
public sealed record TeleportTarget(string Region, float X, float Y, float Z)
{
    public const float RegionSize = 256f;
    public const float MaxHeight = 4096f;

    public static bool TryCreate(string? region, float x, float y, float z, out TeleportTarget? target, out string? error)
    {
        target = null;
        region = region?.Trim();
        if (string.IsNullOrEmpty(region))
        {
            error = "Give a region name to teleport to.";
            return false;
        }
        if (x < 0 || x >= RegionSize || y < 0 || y >= RegionSize)
        {
            error = $"x and y must be between 0 and {RegionSize - 1} (they're positions inside the region).";
            return false;
        }
        if (z < 0 || z > MaxHeight)
        {
            error = $"z must be between 0 and {MaxHeight}.";
            return false;
        }
        error = null;
        target = new TeleportTarget(region, x, y, z);
        return true;
    }

    /// <summary>
    /// Parses a SLURL or "Region/x/y/z" text, e.g. "http://maps.secondlife.com/secondlife/Ahern/128/128/30",
    /// "secondlife:///app/teleport/Da%20Boom/10/20/30", or "Ahern/128/128".
    /// </summary>
    public static bool TryParse(string? text, out TeleportTarget? target, out string? error)
    {
        target = null;
        var value = (text ?? "").Trim();
        foreach (var prefix in new[] { "http://maps.secondlife.com/secondlife/", "https://maps.secondlife.com/secondlife/",
                     "http://slurl.com/secondlife/", "https://slurl.com/secondlife/", "secondlife:///app/teleport/", "secondlife://" })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[prefix.Length..];
                break;
            }
        }
        value = Uri.UnescapeDataString(value.Replace('+', ' ')).TrimEnd('/');
        var parts = value.Split('/');
        float[] coords = [128, 128, 30];
        for (var i = 1; i < parts.Length && i <= 3; i++)
        {
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out coords[i - 1]))
            {
                error = $"'{text}' isn't a location I understand. Use a SLURL or 'Region/x/y/z'.";
                return false;
            }
        }
        if (parts.Length > 4)
        {
            error = $"'{text}' isn't a location I understand. Use a SLURL or 'Region/x/y/z'.";
            return false;
        }
        return TryCreate(parts[0], coords[0], coords[1], coords[2], out target, out error);
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Region} ({X:0}, {Y:0}, {Z:0})");
}
