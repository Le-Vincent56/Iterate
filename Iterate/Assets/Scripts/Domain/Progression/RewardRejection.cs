namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Why a reward could not be planned or a choice could not be taken. Every planning rejection
    /// rejects the whole package, so none of them describes a partially awarded reward.
    /// </summary>
    public enum RewardRejection
    {
        None,
        ContentMissing,
        PoolMissing,
        PoolEmpty,
        CapacityBelowUsage,
        UnknownComponent,
        ComponentNotPending,
        NotAPoolMember,
        InstallationRejected
    }
}