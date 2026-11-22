using System.Globalization;
using System.Text;
using LibreMetaverse;

namespace SecondSight.Core.Travel;

/// <summary>
/// Where a teleport offer points. Second Life puts this in the offer's binary bucket as
/// "globalX|globalY|localX|localY|localZ|lookX|lookY|lookZ|access", the same text the
/// official viewer reads to show the destination.
/// </summary>
public sealed record LureLocation(ulong RegionHandle, uint GlobalX, uint GlobalY, Vector3 Position)
{
    public static bool TryParse(byte[]? bucket, out LureLocation? location)
    {
        location = null;
        if (bucket == null || bucket.Length == 0) return false;

        var text = Encoding.UTF8.GetString(bucket).TrimEnd('\0').Trim();
        var parts = text.Split('|');
        if (parts.Length < 5) return false;

        if (!uint.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var gx) ||
            !uint.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var gy))
            return false;

        var local = new float[3];
        for (var i = 0; i < 3; i++)
        {
            if (!float.TryParse(parts[2 + i], NumberStyles.Float, CultureInfo.InvariantCulture, out local[i])) return false;
        }

        // Region handles are built from the region's corner, in whole 256 m steps.
        var cornerX = gx - gx % 256;
        var cornerY = gy - gy % 256;
        var handle = ((ulong)cornerX << 32) | cornerY;
        location = new LureLocation(handle, gx, gy, new Vector3(local[0], local[1], local[2]));
        return true;
    }
}
