using System.Collections.Generic;
using Iterate.Domain.Determinism;
using Iterate.Domain.Values;

namespace Iterate.Application.Persistence
{
    /// <summary>
    /// The onboarding checkpoint: everything needed to put the player back where they were, as pure
    /// data naming content by identity rather than holding it. No Process state is here — a retry
    /// recreates the Process from its authored configuration, so what a checkpoint preserves is the
    /// Session either side of it.
    ///
    /// Both revisions are carried so a checkpoint written by one build can be refused rather than
    /// misread by another: the catalog's, because content identities resolve against it, and the random
    /// service's, because a deterministic retry that drew differently would not be the same retry.
    /// </summary>
    /// <param name="CatalogRevision">The catalog revision the Session was built against.</param>
    /// <param name="RandomServiceRevision">The deterministic random service's revision.</param>
    /// <param name="SystemIdentity">The System being played.</param>
    /// <param name="SessionIdentity">The Session's own identity.</param>
    /// <param name="SessionSeedIdentity">The seed identity every Session decision derives from.</param>
    /// <param name="Process">The Process the player was entering.</param>
    /// <param name="Route">The route taken to it, or null when the Process is not on one.</param>
    /// <param name="ConsecutiveFailures">How many times this checkpoint has been failed in a row.</param>
    /// <param name="LastAllocatedInstance">The ordinal of the most recently allocated instance identity.</param>
    /// <param name="Ordinals">The occurrence-ordinal counters, in their stable order.</param>
    /// <param name="Repository">The Repository entries in acquisition order.</param>
    /// <param name="Branch">The confirmed Active Branch, or null when none has been confirmed.</param>
    /// <param name="Economy">The Session's economy.</param>
    public sealed record CheckpointState(
        string CatalogRevision,
        string RandomServiceRevision,
        string SystemIdentity,
        string SessionIdentity,
        string SessionSeedIdentity,
        ProcessID Process,
        RouteID? Route,
        int ConsecutiveFailures,
        int LastAllocatedInstance,
        IReadOnlyList<OrdinalCounter> Ordinals,
        IReadOnlyList<CheckpointRepositoryItem> Repository,
        IReadOnlyList<InstanceID> Branch,
        CheckpointEconomy Economy
    )
    {
        /// <summary>
        /// Derives the checkpoint after one more failed attempt at it. The count belongs to this
        /// checkpoint rather than to the player, so it resets when the player moves past it.
        /// </summary>
        /// <returns>The derived checkpoint.</returns>
        public CheckpointState RecordFailure() => this with { ConsecutiveFailures = ConsecutiveFailures + 1 };
    }
}