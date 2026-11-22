using System.ComponentModel;
using ModelContextProtocol.Server;
using SecondSight.Core.Perception;

namespace SecondSight.Tools;

/// <summary>MCP tools for seeing what is around the bot.</summary>
[McpServerToolType]
public sealed class PerceptionTools(IPerception perception)
{
    public const float DefaultRadius = 30;
    public const float MaxRadius = 128;
    public const int DefaultMaxObjects = 25;
    public const int MaxObjects = 100;

    [McpServerTool(Name = "describe_surroundings", ReadOnly = true), Description(
        "Describe what is around the bot: the region and parcel, where your owner is (distance and compass " +
        "direction), other avatars in the region, and nearby objects, nearest first, with their size, whether " +
        "they can be sat on or touched, and an id for inspect_object. Directions are compass points: north is +Y, east is +X.")]
    public async Task<string> DescribeSurroundings(
        [Description("How far to look for objects, in meters (1-128). Default 30.")] float radius = DefaultRadius,
        [Description("Most objects to list (1-100). Default 25.")] int maxObjects = DefaultMaxObjects,
        CancellationToken cancellationToken = default)
    {
        var (r, n) = Clamp(radius, maxObjects);
        return (await perception.DescribeSurroundingsAsync(r, n, cancellationToken)).Message;
    }

    [McpServerTool(Name = "inspect_object", ReadOnly = true), Description(
        "Look closely at one object or avatar near the bot, by the id from describe_surroundings: its name, " +
        "description, floating text, owner, creator, size, where it is, whether it is for sale, and what clicking it does.")]
    public async Task<string> InspectObject(
        [Description("The object's or avatar's id (UUID) from describe_surroundings.")] string id,
        CancellationToken cancellationToken = default)
        => (await perception.InspectAsync(id, cancellationToken)).Message;

    internal static (float Radius, int MaxObjects) Clamp(float radius, int maxObjects)
    {
        var r = float.IsFinite(radius) ? Math.Clamp(radius, 1, MaxRadius) : DefaultRadius;
        return (r, Math.Clamp(maxObjects, 1, MaxObjects));
    }
}
