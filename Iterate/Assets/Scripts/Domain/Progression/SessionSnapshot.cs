using System.Collections.Generic;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// An immutable view of everything a Session carries between Processes: its identities, how far its
    /// allocation counter and occurrence ordinals have run, its Repository, the confirmed Active Branch
    /// and its economy. A Session rebuilt from a snapshot mints the identities and draws the decisions
    /// the uninterrupted Session would have, which is what makes a checkpoint a continuation rather than
    /// a copy. No Process state is here: a checkpoint is taken between Processes, never inside one.
    /// </summary>
    /// <param name="SystemIdentity">The System this Session is playing.</param>
    /// <param name="SessionIdentity">The Session's own identity.</param>
    /// <param name="SessionSeedIdentity">The seed identity every Session decision derives from.</param>
    /// <param name="CatalogRevision">The catalog revision the Session was built against.</param>
    /// <param name="LastAllocatedInstance">The ordinal of the most recently allocated instance identity.</param>
    /// <param name="Ordinals">The occurrence-ordinal counters, in their stable order.</param>
    /// <param name="Repository">The Repository entries in acquisition order.</param>
    /// <param name="ConfirmedBranch">The confirmed Active Branch, or null when none has been confirmed.</param>
    /// <param name="Economy">The Session's economy.</param>
    public sealed record SessionSnapshot(
        string SystemIdentity,
        string SessionIdentity,
        string SessionSeedIdentity,
        string CatalogRevision,
        int LastAllocatedInstance,
        IReadOnlyList<OrdinalCounter> Ordinals,
        IReadOnlyList<RepositoryEntry> Repository,
        ActiveBranchConfiguration ConfirmedBranch,
        EconomySnapshot Economy
    );
}