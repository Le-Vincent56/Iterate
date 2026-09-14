namespace Iterate.Domain.Progression
{
    /// <summary>
    /// What a numbered shop slot is currently offering. A Sold slot is empty rather than gone, so a
    /// reroll refills it (spec section 6); an Exhausted slot had nothing unseen left to show.
    /// </summary>
    public enum ShopSlotState
    {
        Available,
        Sold,
        Exhausted
    }
}