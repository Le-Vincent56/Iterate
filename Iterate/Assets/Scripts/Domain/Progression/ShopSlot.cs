namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One numbered slot. The number is stable for the shop's life, so a pin or a reroll refers to a
    /// position rather than to whatever currently occupies it.
    /// </summary>
    /// <param name="Number">The one-based slot number.</param>
    /// <param name="Offer">The offer currently in the slot.</param>
    /// <param name="State">Whether it is available, sold or exhausted.</param>
    /// <param name="Pinned">Whether a reroll leaves it alone.</param>
    public sealed record ShopSlot(
        int Number,
        ShopSlotOffer Offer,
        ShopSlotState State,
        bool Pinned
    );
}