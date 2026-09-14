namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The outcome of a standalone destruction.
    /// </summary>
    /// <param name="Succeeded">Whether the Dependency was destroyed.</param>
    /// <param name="Rejection">Why it was refused; None on success.</param>
    /// <param name="Record">The committed transaction; null on refusal.</param>
    /// <param name="Refund">The Tokens refunded.</param>
    public sealed record DependencyDestructionResult(
        bool Succeeded,
        DependencyRejection Rejection,
        TransactionRecord Record,
        int Refund
    );
}