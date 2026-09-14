namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The outcome of a routine purchase. Exactly one of the two outputs is populated: an item
    /// purchase acquires a Repository entry, a Utility purchase leaves a commitment.
    /// </summary>
    /// <param name="Succeeded">Whether the purchase happened.</param>
    /// <param name="Rejection">Why it was refused; None on success.</param>
    /// <param name="Record">The committed transaction; null on refusal.</param>
    /// <param name="Acquired">The acquired entry, for an item purchase.</param>
    /// <param name="Committed">The commitment, for a Utility purchase.</param>
    public sealed record PurchaseResult(
        bool Succeeded,
        ShopRejection Rejection,
        TransactionRecord Record,
        RepositoryEntry Acquired,
        UtilityCommitment Committed
    );
}