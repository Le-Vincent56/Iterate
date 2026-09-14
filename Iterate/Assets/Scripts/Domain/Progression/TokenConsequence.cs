using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Tokens moved by a transaction.
    /// </summary>
    /// <param name="Amount">The Tokens moved.</param>
    /// <param name="IsDebit">Whether the movement spent Tokens.</param>
    /// <param name="Basis">Why the movement happened.</param>
    /// <param name="BalanceAfter">The balance the movement left behind.</param>
    public sealed record TokenConsequence(
        TokenAmount Amount,
        bool IsDebit,
        TokenBasis Basis,
        TokenAmount BalanceAfter
    ) : TransactionConsequence;
}