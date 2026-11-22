using LibreMetaverse;
using SecondSight.Core.Chat;

namespace SecondSight.Core.Travel;

public enum LureAction
{
    /// <summary>Not a teleport-related IM.</summary>
    None,
    /// <summary>The owner offered a teleport: accept it.</summary>
    AcceptOffer,
    /// <summary>Someone else offered a teleport: decline it.</summary>
    DeclineOffer,
    /// <summary>The owner asked the bot for a teleport offer: send one.</summary>
    SendOfferToOwner,
    /// <summary>Someone else asked the bot for a teleport offer: ignore it.</summary>
    IgnoreRequest,
}

/// <summary>Decides what to do with teleport offers and teleport requests. Only the owner can move the bot this way.</summary>
public static class LureDecision
{
    public static LureAction Decide(InstantMessage im, UUID selfId, OwnerIdentity owner)
    {
        if (im.FromAgentID == selfId) return LureAction.None;
        var isOwner = owner.IsOwner(im.FromAgentID, im.FromAgentName, isAvatar: true);
        return im.Dialog switch
        {
            InstantMessageDialog.RequestTeleport => isOwner ? LureAction.AcceptOffer : LureAction.DeclineOffer,
            InstantMessageDialog.GodLikeRequestTeleport => LureAction.AcceptOffer, // Linden staff; can't be refused anyway
            InstantMessageDialog.RequestLure => isOwner ? LureAction.SendOfferToOwner : LureAction.IgnoreRequest,
            _ => LureAction.None,
        };
    }
}
