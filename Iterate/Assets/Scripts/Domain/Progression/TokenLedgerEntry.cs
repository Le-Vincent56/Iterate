using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One accepted movement of Tokens, with the basis it happened under and the balance it left
    /// behind. Mirrors <see cref="LedgerEntry"/>, which does the same for Bytes, and adds the basis
    /// because a Session's Tokens move for several distinguishable reasons where its Bytes do not.
    /// </summary>
    /// <param name="Amount">The Tokens moved.</param>
    /// <param name="IsDebit">Whether the movement spent Tokens rather than granting them.</param>
    /// <param name="Basis">Why the movement happened.</param>
    /// <param name="Reference">The offer, content or transaction the movement refers to.</param>
    /// <param name="Balance">The balance after the movement.</param>
    public sealed record TokenLedgerEntry(
        TokenAmount Amount,
        bool IsDebit,
        TokenBasis Basis,
        string Reference,
        TokenAmount Balance
    );
}