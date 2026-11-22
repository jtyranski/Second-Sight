using SecondSight.Core.Session;

namespace SecondSight.Core.Travel;

/// <summary>Moving the bot between places, and sharing teleports with the owner.</summary>
public interface ITravel
{
    Task<SessionResult> TeleportAsync(TeleportTarget target, CancellationToken cancellationToken);
    Task<SessionResult> GoHomeAsync(CancellationToken cancellationToken);

    /// <summary>Sends the owner a teleport offer to the bot's current location.</summary>
    Task<SessionResult> OfferTeleportToOwnerAsync(string? message, CancellationToken cancellationToken);
}
