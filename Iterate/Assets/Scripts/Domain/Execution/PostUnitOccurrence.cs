using System;
using Iterate.Domain.Compilation;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One closed runtime unit offered to the effect engine at the post-unit consequence-and-evidence
    /// band: the unit, its completion event, the final disposition, ownership with the owning host
    /// instance, the Structure context it executed within, the offering branch's lineage, and the four
    /// facts the Patch post-unit qualifiers read — the unit's source position, whether it is the final
    /// occupied player line, the retained Condition result it executed inside, and whether it opened
    /// adjacent to a successful Score-increasing player Instruction.
    /// </summary>
    /// <param name="Unit">The closed runtime unit.</param>
    /// <param name="CompletionEvent">The unit's completion event.</param>
    /// <param name="FinalDisposition">The unit's final disposition.</param>
    /// <param name="Ownership">The unit's ownership classification.</param>
    /// <param name="HostInstance">The owning host instance; non-null exactly when player-owned.</param>
    /// <param name="StructureContext">The Structure context, or null when top-level.</param>
    /// <param name="BranchLineage">The offering branch's effect-origin lineage; never null.</param>
    /// <param name="Position">The unit's source position, or null for a source-less unit.</param>
    /// <param name="IsFinalOccupiedPlayerLine">Whether the unit is the final occupied player-controlled line.</param>
    /// <param name="ConditionResult">The retained enclosing Condition result, or null when not inside one.</param>
    /// <param name="AdjacentAfterSuccessfulScore">Whether the unit opened adjacent to a successful Score-increasing player Instruction.</param>
    public sealed record PostUnitOccurrence(
        RuntimeUnitID Unit,
        TraceEventID CompletionEvent,
        EventDisposition FinalDisposition,
        OwnershipClassification Ownership,
        InstanceID? HostInstance,
        StructureContext StructureContext,
        EffectOriginLineage BranchLineage,
        SourcePosition? Position,
        bool IsFinalOccupiedPlayerLine,
        ConditionOutcome? ConditionResult,
        bool AdjacentAfterSuccessfulScore
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
        /// Whether the unit is the final occupied player-controlled line. Validated at construction:
        /// a Core-owned unit is never a player line.
        /// </summary>
        public bool IsFinalOccupiedPlayerLine { get; } = RequireFinalLinePairing(Ownership, IsFinalOccupiedPlayerLine);

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
        /// Validates that only a player-owned unit is the final occupied player line.
        /// </summary>
        /// <param name="ownership">The unit's ownership classification.</param>
        /// <param name="isFinalOccupiedPlayerLine">Whether the unit is the final occupied player line.</param>
        /// <returns>The flag unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when a Core-owned unit claims the final player line.</exception>
        private static bool RequireFinalLinePairing(
            OwnershipClassification ownership,
            bool isFinalOccupiedPlayerLine
        )
        {
            if (isFinalOccupiedPlayerLine && ownership != OwnershipClassification.PlayerOwned)
                throw new ArgumentException("A Core-owned unit is never the final occupied player line.", nameof(isFinalOccupiedPlayerLine));

            return isFinalOccupiedPlayerLine;
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