using System.Globalization;

namespace SecondSight.Core.Session;

/// <summary>Turns "last", "home", or "Region Name/x/y/z" into the start string the login server expects.</summary>
public static class StartLocationParser
{
    public static bool TryParse(string? input, out string start, out string? error)
    {
        start = "last";
        error = null;
        var value = (input ?? "").Trim();
        if (value.Length == 0 || value.Equals("last", StringComparison.OrdinalIgnoreCase)) return true;
        if (value.Equals("home", StringComparison.OrdinalIgnoreCase)) { start = "home"; return true; }

        var parts = value.Split('/');
        var region = parts[0].Trim();
        if (region.Length == 0)
        {
            error = $"Start location '{value}' has no region name. Use 'last', 'home', or 'Region Name/x/y/z'.";
            return false;
        }

        int[] coords = [128, 128, 30];
        for (var i = 1; i < parts.Length; i++)
        {
            if (i > 3 || !int.TryParse(parts[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out coords[i - 1]))
            {
                error = $"Start location '{value}' is not valid. Use 'last', 'home', or 'Region Name/x/y/z'.";
                return false;
            }
        }

        start = $"uri:{region}&{coords[0]}&{coords[1]}&{coords[2]}";
        return true;
    }
}
