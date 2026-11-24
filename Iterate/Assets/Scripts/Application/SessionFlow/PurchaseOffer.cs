namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Buys the routine offer in a shop slot. Patches and Dependencies have their own commands,
    /// because they install as they are bought, and so do the item Services, which are not slot offers.
    /// </summary>
    /// <param name="Slot">The shop slot to buy from.</param>
    public sealed record PurchaseOffer(int Slot) : ISessionCommand;
}