using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One accepted movement of Bytes, with the balance it left behind, so a Process's Byte history
    /// reads without replaying it.
    /// </summary>
    /// <param name="Amount">The Bytes moved.</param>
    /// <param name="IsDebit">Whether the movement spent Bytes rather than granting them.</param>
    /// <param name="Reason">Why the movement happened.</param>
    /// <param name="BalanceAfter">The balance after the movement.</param>
    public sealed record LedgerEntry(
        ByteAmount Amount,
        bool IsDebit,
        string Reason,
        ByteAmount BalanceAfter
    );
}