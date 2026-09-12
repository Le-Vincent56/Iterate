namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One Repository holding: the item, how it was acquired, whether it is starter-protected, and the
    /// acquisition-order suffix that distinguishes two copies of one definition in the interface. The
    /// suffix is allocated per definition and is never renumbered or reissued.
    /// </summary>
    /// <param name="Item">The held item.</param>
    /// <param name="Origin">How the item was acquired.</param>
    /// <param name="IsStarterProtected">Whether the entry is protected from deletion.</param>
    /// <param name="Suffix">The per-definition acquisition-order suffix, from one.</param>
    public sealed record RepositoryEntry(
        RepositoryItem Item,
        AcquisitionOrigin Origin,
        bool IsStarterProtected,
        int Suffix
    );
}