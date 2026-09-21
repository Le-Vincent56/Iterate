using System.Collections.Generic;
using Iterate.Domain.Progression;

namespace Iterate.Application.Persistence
{
    /// <summary>
    /// The Session's economy as a checkpoint holds it: the Token balance rather than the ledger's
    /// entries, the RAM capacity and what fills it, the commitments and grants outstanding, and the
    /// per-kind transaction counts so restored identities continue rather than restart.
    /// </summary>
    /// <param name="Tokens">The Token balance.</param>
    /// <param name="RAMCapacity">The Dependency rack's capacity.</param>
    /// <param name="Starter">The Dependency installed at Session start.</param>
    /// <param name="Dependencies">The installed Dependencies, in installation order.</param>
    /// <param name="Utilities">The Utility commitments, in commitment order.</param>
    /// <param name="Grants">The outstanding Patch grants, in grant order.</param>
    /// <param name="TransactionCounters">The per-kind transaction counts, ordered by kind.</param>
    public sealed record CheckpointEconomy(
        int Tokens,
        int RAMCapacity,
        CheckpointStarter Starter,
        IReadOnlyList<CheckpointDependency> Dependencies,
        IReadOnlyList<CheckpointUtility> Utilities,
        IReadOnlyList<CheckpointGrant> Grants,
        IReadOnlyList<TransactionCounter> TransactionCounters
    );
}