using Iterate.Domain.Content;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One granted component, resolved to the concrete consequence it will have. Holding the resolved
    /// definition and pool is what lets Apply commit without a catalog and without failing.
    /// </summary>
    /// <param name="ComponentIndex">The component's authored index in the package.</param>
    /// <param name="Component">The authored component.</param>
    /// <param name="Kind">What applying it will do.</param>
    /// <param name="Basis">The Token basis for a credit; null otherwise.</param>
    /// <param name="Amount">The amount for a credit or a capacity target; zero otherwise.</param>
    /// <param name="Content">The resolved content definition, where one is granted.</param>
    /// <param name="Pool">The resolved pool, for a choice.</param>
    /// <param name="Cached">Whether a granted Patch is cached.</param>
    public sealed record PlannedReward(
        int ComponentIndex,
        RewardComponent Component,
        PlannedRewardKind Kind,
        TokenBasis? Basis,
        int Amount,
        ContentDefinition Content,
        PoolDefinition Pool,
        bool Cached
    );
}