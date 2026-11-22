using SecondSight.Core.Session;

namespace SecondSight.Core.Perception;

/// <summary>What the bot can see around it.</summary>
public interface IPerception
{
    Task<SessionResult> DescribeSurroundingsAsync(float radius, int maxObjects, CancellationToken cancellationToken);

    /// <summary>Details for one object or avatar, by UUID or local ID.</summary>
    Task<SessionResult> InspectAsync(string id, CancellationToken cancellationToken);
}
