namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One shop slot replaced by a reroll.
    /// </summary>
    /// <param name="Slot">The slot number.</param>
    /// <param name="PreviousContentID">The content that left the slot.</param>
    /// <param name="ContentID">The content that took it.</param>
    public sealed record OfferReplaced(int Slot, string PreviousContentID, string ContentID) : TransactionConsequence;
}