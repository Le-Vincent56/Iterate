using System;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One closed runtime unit offered to the effect engine at the post-unit consequence-and-evidence
    /// band: the unit, its completion event, the final disposition, ownership with the owning host
    /// instance, the Structure context it executed within, and the offering branch's lineage.
    /// </summary>
    /// <param name="Unit">The closed runtime unit.</param>
    /// <param name="CompletionEvent">The unit's completion event.</param>
    /// <param name="FinalDisposition">The unit's final disposition.</param>
    /// <param name="Ownership">The unit's ownership classification.</param>
    /// <param name="HostInstance">The owning host instance; non-null exactly when player-owned.</param>
    /// <param name="StructureContext">The Structure context, or null when top-level.</param>
    /// <param name="BranchLineage">The offering branch's effect-origin lineage; never null.</param>
    public sealed record PostUnitOccurrence(
        RuntimeUnitID Unit,
        TraceEventID CompletionEvent,
        EventDisposition FinalDisposition,
        OwnershipClassification Ownership,
        InstanceID? HostInstance,
        StructureContext StructureContext,
        EffectOriginLineage BranchLineage
    )
    {
        /// <summary>
        /// The owning host instance. Validated at construction: non-null exactly when the ownership
        /// is player-owned.
        /// </summary>
        public InstanceID? HostInstance { get; } = RequireHostPairing(Ownership, HostInstance);

        /// <summary>
        /// The offering branch's effect-origin lineage. Validated non-null at construction.
        /// </summary>
        public EffectOriginLineage BranchLineage { get; } = RequireLineage(BranchLineage);

        /// <summary>
        /// Validates that the host instance is present exactly when the unit is player-owned.
        /// </summary>
        /// <param name="ownership">The unit's ownership classification.</param>
        /// <param name="hostInstance">The candidate host instance.</param>
        /// <returns>The host instance unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when presence and ownership disagree.</exception>
        private static InstanceID? RequireHostPairing(
            OwnershipClassification ownership,
            InstanceID? hostInstance
        )
        {
            if (ownership == OwnershipClassification.PlayerOwned && hostInstance == null)
                throw new ArgumentException("A player-owned occurrence requires a host instance.", nameof(hostInstance));

            if (ownership == OwnershipClassification.CoreOwned && hostInstance != null)
                throw new ArgumentException("A Core-owned occurrence carries no host instance.", nameof(hostInstance));

            return hostInstance;
        }

        /// <summary>
        /// Validates that the branch lineage is present.
        /// </summary>
        /// <param name="branchLineage">The candidate lineage.</param>
        /// <returns>The lineage unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the lineage is null.</exception>
        private static EffectOriginLineage RequireLineage(EffectOriginLineage branchLineage)
        {
            if (branchLineage == null)
                throw new ArgumentException("An occurrence requires a branch lineage.", nameof(branchLineage));

            return branchLineage;
        }
    }
}