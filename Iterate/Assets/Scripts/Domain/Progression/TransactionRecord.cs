using System.Collections.Generic;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One committed economy transaction: what kind it was, its Session-unique identity, and its
    /// consequences in commit order. A transaction that was refused produces no record at all, which
    /// is what "no events on failure" means at this layer.
    /// </summary>
    /// <param name="Kind">What the transaction did.</param>
    /// <param name="TransactionIdentity">The per-kind identity, "kind:n".</param>
    /// <param name="Consequences">What it did, in commit order.</param>
    public sealed record TransactionRecord(
        TransactionKind Kind,
        string TransactionIdentity,
        IReadOnlyList<TransactionConsequence> Consequences
    );
}