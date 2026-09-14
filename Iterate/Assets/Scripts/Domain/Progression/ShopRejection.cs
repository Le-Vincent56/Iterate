namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Why a shop operation was refused. Reroll validation checks these in declaration order, so a
    /// player is told what is actually wrong first rather than whichever check happens to run last.
    /// </summary>
    public enum ShopRejection
    {
        None,
        ContentMissing,
        PoolMissing,
        UnknownSlot,
        SlotNotAvailable,
        PinningDisabled,
        RerollsDisabled,
        RerollLimitReached,
        NoUnpinnedSlot,
        InsufficientTokens,
        InsufficientUnseenStock,
        NotARoutinePurchase,
        NotAPatchOffer,
        NotADependencyOffer,
        DependenciesNotOffered,
        ServicesNotOffered,
        ShopClosed
    }
}