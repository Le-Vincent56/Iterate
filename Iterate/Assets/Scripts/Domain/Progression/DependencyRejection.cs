namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Why a rack or Dependency-installation operation was refused. Shared by the rack and the
    /// installer so a composite installation surfaces the rack's own reason unchanged.
    /// </summary>
    public enum DependencyRejection
    {
        None,
        NotADependency,
        UnknownInstance,
        StarterPermanent,
        DuplicateDestroyTarget,
        InsufficientRAM,
        InsufficientTokens,
        CapacityBelowUsage,
        DependenciesNotOffered
    }
}