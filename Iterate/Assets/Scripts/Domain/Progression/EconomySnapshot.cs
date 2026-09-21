using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// An immutable view of the Session's economy: the Token balance rather than the ledger's entries,
    /// the RAM capacity and what is installed in it, the commitments and grants outstanding, and the
    /// per-kind transaction counts. Entries and records are deliberately absent — they describe what
    /// happened during a sitting, not what the Session holds.
    /// </summary>
    /// <param name="Tokens">The Token balance.</param>
    /// <param name="Starter">The Dependency installed at Session start.</param>
    /// <param name="RAMCapacity">The rack's capacity.</param>
    /// <param name="Installed">The installed Dependencies, in installation order.</param>
    /// <param name="Utilities">The Utility commitments, in commitment order.</param>
    /// <param name="Grants">The Patch grants awaiting a host, in grant order.</param>
    /// <param name="TransactionCounters">The per-kind transaction counts, ordered by kind.</param>
    public sealed record EconomySnapshot(
        TokenAmount Tokens,
        DependencyInstance Starter,
        int RAMCapacity,
        IReadOnlyList<InstalledDependency> Installed,
        IReadOnlyList<UtilityCommitment> Utilities,
        IReadOnlyList<PatchGrant> Grants,
        IReadOnlyList<TransactionCounter> TransactionCounters
    );
}