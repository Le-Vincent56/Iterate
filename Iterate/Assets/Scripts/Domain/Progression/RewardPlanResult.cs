namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The outcome of planning. A rejection names the first component that could not resolve and
    /// carries no plan, because a package resolves entirely or not at all.
    /// </summary>
    /// <param name="Succeeded">Whether the package resolved.</param>
    /// <param name="Rejection">Why it did not; None on success.</param>
    /// <param name="Plan">The resolved plan; null on rejection.</param>
    public sealed record RewardPlanResult(bool Succeeded, RewardRejection Rejection, RewardPlan Plan);
}