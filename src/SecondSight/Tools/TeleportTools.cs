using System.ComponentModel;
using ModelContextProtocol.Server;
using SecondSight.Core.Travel;

namespace SecondSight.Tools;

/// <summary>MCP tools for teleporting the bot and sharing teleports with the owner.</summary>
[McpServerToolType]
public sealed class TeleportTools(ITravel travel)
{
    [McpServerTool(Name = "teleport"), Description(
        "Teleport the bot to a region and position. Give either a region name with optional x/y/z " +
        "(positions inside the region, 0-255; defaults to the center at 128, 128, 30), or a SLURL " +
        "such as http://maps.secondlife.com/secondlife/Region/128/128/30. Only do this when your owner asks.")]
    public async Task<string> Teleport(
        [Description("Region name, or a SLURL / 'Region/x/y/z' text.")] string region,
        [Description("X position in the region (0-255).")] float? x = null,
        [Description("Y position in the region (0-255).")] float? y = null,
        [Description("Height (z).")] float? z = null,
        CancellationToken cancellationToken = default)
    {
        TeleportTarget? target;
        string? error;
        var ok = x is null && y is null && z is null
            ? TeleportTarget.TryParse(region, out target, out error)
            : TeleportTarget.TryCreate(region, x ?? 128, y ?? 128, z ?? 30, out target, out error);
        if (!ok) return error!;

        var result = await travel.TeleportAsync(target!, cancellationToken);
        return result.Message;
    }

    [McpServerTool(Name = "teleport_home"), Description("Teleport the bot to its home location.")]
    public async Task<string> TeleportHome(CancellationToken cancellationToken = default)
        => (await travel.GoHomeAsync(cancellationToken)).Message;

    [McpServerTool(Name = "offer_teleport"), Description(
        "Send your owner a teleport offer to the bot's current location, so they can join the bot. " +
        "The owner has to accept it in their viewer.")]
    public async Task<string> OfferTeleport(
        [Description("Optional short message shown with the offer.")] string? message = null,
        CancellationToken cancellationToken = default)
        => (await travel.OfferTeleportToOwnerAsync(message, cancellationToken)).Message;
}
