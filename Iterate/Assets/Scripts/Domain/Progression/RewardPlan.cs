using System.Collections.Generic;
using Iterate.Domain.Content;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Everything a package will award at the tier reached, resolved and ordered but not yet applied.
    /// This is what a Reward Summary reads: it describes the whole award without granting any of it.
    /// </summary>
    /// <param name="Package">The package being resolved.</param>
    /// <param name="Reached">The tier reached, or null.</param>
    /// <param name="Planned">The granted components, in authored order.</param>
    public sealed record RewardPlan(
        RewardPackageDefinition Package,
        RewardTier? Reached,
        IReadOnlyList<PlannedReward> Planned
    );
}