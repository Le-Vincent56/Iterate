namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The outcome of a Service. A refusal carries no record.
    /// </summary>
    /// <param name="Succeeded">Whether the Service was performed.</param>
    /// <param name="Rejection">Why it was refused; None on success.</param>
    /// <param name="Record">The committed transaction; null on refusal.</param>
    /// <param name="Entry">The entry created by a duplication; null otherwise.</param>
    public sealed record ServiceResult(
        bool Succeeded,
        ServiceRejection Rejection,
        TransactionRecord Record,
        RepositoryEntry Entry
    );
}