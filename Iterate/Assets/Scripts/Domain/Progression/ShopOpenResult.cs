namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The outcome of opening a shop. A shop whose content the catalog cannot supply does not open at
    /// all, so a player never sees a slot that cannot be bought.
    /// </summary>
    /// <param name="Succeeded">Whether the shop opened.</param>
    /// <param name="Rejection">Why it did not; None on success.</param>
    /// <param name="Shop">The opened shop; null on refusal.</param>
    public sealed record ShopOpenResult(bool Succeeded, ShopRejection Rejection, ShopState Shop);
}