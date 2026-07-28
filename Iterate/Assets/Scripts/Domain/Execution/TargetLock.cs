using System;
using Iterate.Domain.Compilation;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// A committed target lock: the player Instruction slot a creator locked, its host instance, the
    /// Structure context the host executed within, the quantity event that qualified the lock, the
    /// locked unit, and the host's retained Condition evaluation outcome. Written at the lock update's
    /// quantity-change commitment and overwritten by each later qualifying commitment, so the stored
    /// value is always the most recent qualifying host. At this content the lock is written at
    /// quantity-change commitment, which precedes the unit's closure; it tracks executions that
    /// resolve successfully, and nothing can fail a unit after its primary operation resolves here, so
    /// the two moments are equivalent — child viii (abort semantics) must revisit this equivalence when
    /// a post-operation failure becomes representable.
    /// Re-checked when the Process-counter wiring landed: the equivalence still holds. The Heat
    /// pre-check sits before primary resolution and the abort path reads no lock, so neither the
    /// intervention nor the unwind can observe a lock between the two timings.
    /// </summary>
    /// <param name="LockedSlot">The locked player Instruction slot.</param>
    /// <param name="HostInstance">The locked host's instance identity.</param>
    /// <param name="InheritedContext">The host's Structure context, or null when top-level.</param>
    /// <param name="TriggeringEvent">The quantity event that qualified the lock.</param>
    /// <param name="LockedUnit">The locked source execution's runtime unit.</param>
    /// <param name="RetainedConditionResult">The locked host's enclosing Condition evaluation result at lock time, or null outside a Condition; False for a rescued-from-FALSE host — the frozen Structure context carries the evaluation identity but not its True/False result, so the Burst descendant's closure needs this to re-offer the host's retained succeeding-Condition context (CAB-EVT-427/429/439).</param>
    public sealed record TargetLock(
        SourceSlot LockedSlot,
        InstanceID HostInstance,
        StructureContext InheritedContext,
        TraceEventID TriggeringEvent,
        RuntimeUnitID LockedUnit,
        ConditionOutcome? RetainedConditionResult
    )
    {
        /// <summary>
        /// The locked player Instruction slot. Validated at construction: an Instruction or
        /// contained-Instruction slot carrying an occupant.
        /// </summary>
        public SourceSlot LockedSlot { get; } = RequireInstructionSlot(LockedSlot);

        /// <summary>
        /// Validates that the locked slot is a player Instruction slot carrying an occupant.
        /// </summary>
        /// <param name="lockedSlot">The candidate slot.</param>
        /// <returns>The slot unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the slot is null or not a player Instruction slot.</exception>
        private static SourceSlot RequireInstructionSlot(SourceSlot lockedSlot)
        {
            if (lockedSlot == null)
                throw new ArgumentException("A target lock requires a locked slot.", nameof(lockedSlot));

            if (lockedSlot.Kind != SourceSlotKind.Instruction && lockedSlot.Kind != SourceSlotKind.ContainedInstruction)
                throw new ArgumentException("A target lock's slot must be a player Instruction slot.", nameof(lockedSlot));

            return lockedSlot;
        }
    }
}