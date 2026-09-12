using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The typed outcome of a Byte movement. A refused debit changes nothing and writes no entry.
    /// </summary>
    /// <param name="Succeeded">Whether the movement was applied.</param>
    /// <param name="Balance">The balance after the attempt.</param>
    /// <param name="Entry">The written entry; null when refused.</param>
    public readonly record struct LedgerResult(bool Succeeded, ByteAmount Balance, LedgerEntry Entry);
}