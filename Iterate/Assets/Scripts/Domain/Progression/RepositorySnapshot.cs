using System.Collections.Generic;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// An immutable view of the Repository's entries in acquisition order, taken on demand. A snapshot
    /// never changes when the Repository does, so a Branch configuration reads one stable set.
    /// </summary>
    /// <param name="Entries">The entries in acquisition order at the moment of the snapshot.</param>
    public sealed record RepositorySnapshot(IReadOnlyList<RepositoryEntry> Entries);
}