namespace Iterate.Domain.Progression
{
    /// <summary>
    /// What a planned component will do when applied. Distinct from the authored
    /// <c>RewardComponentKind</c>: two authored kinds can plan to one consequence, as Tokens and a
    /// route bonus both plan to a credit under different bases.
    /// </summary>
    public enum PlannedRewardKind
    {
        TokenCredit,
        CapacityTarget,
        GuaranteedContent,
        PatchGrant,
        PoolChoice
    }
}