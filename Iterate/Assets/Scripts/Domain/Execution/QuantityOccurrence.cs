using System;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One finalized quantity change offered to the effect engine: register, actual delta, and origin —
    /// a primary-operation change carries its source ownership, a reaction-caused change carries its
    /// effect origin, so an observing effect can never mistake one for the other — plus the offering
    /// branch's lineage and, when the change is a lockable player source execution, the host slot and
    /// Structure context a target lock records. A boundary-caused change resolves outside any unit and
    /// carries no containing unit.
    /// </summary>
    /// <param name="Unit">The runtime unit containing the change; null only for a boundary-caused change.</param>
    /// <param name="Event">The finalized quantity event this occurrence mirrors.</param>
    /// <param name="CausalDepth">The candidate event's causal depth; never negative.</param>
    /// <param name="Register">The register that changed.</param>
    /// <param name="ActualDelta">The finalized actual delta.</param>
    /// <param name="Ownership">The source ownership; required for a primary-operation change.</param>
    /// <param name="EffectOrigin">The causing effect's instance; non-null exactly when the change is not primary-operation-caused.</param>
    /// <param name="FromPrimaryOperation">Whether the change was produced by the unit's primary operation.</param>
    /// <param name="BranchLineage">The offering branch's effect-origin lineage; never null.</param>
    /// <param name="HostSlot">The lockable player Instruction slot; non-null exactly when the change is a player-owned primary operation.</param>
    /// <param name="SlotContext">The host slot's Structure context; non-null only when the host slot is, itself null at top level.</param>
    public sealed record QuantityOccurrence(
        RuntimeUnitID? Unit,
        TraceEventID Event,
        int CausalDepth,
        CoreRegister Register,
        int ActualDelta,
        OwnershipClassification? Ownership,
        InstanceID? EffectOrigin,
        bool FromPrimaryOperation,
        EffectOriginLineage BranchLineage,
        SourceSlot HostSlot,
        StructureContext SlotContext
    )
    {
        /// <summary>
        /// The runtime unit containing the change. Validated at construction: a null unit requires
        /// the boundary-caused shape — no primary operation and a causing effect origin.
        /// </summary>
        public RuntimeUnitID? Unit { get; } = RequireUnit(Unit, FromPrimaryOperation, EffectOrigin);

        /// <summary>
        /// The candidate event's causal depth. Validated non-negative at construction.
        /// </summary>
        public int CausalDepth { get; } = RequireDepth(CausalDepth);

        /// <summary>
        /// The source ownership. Validated at construction: required for a primary-operation change.
        /// </summary>
        public OwnershipClassification? Ownership { get; } = RequireOwnership(FromPrimaryOperation, Ownership);

        /// <summary>
        /// The causing effect's instance. Validated at construction: non-null exactly when the change
        /// is not primary-operation-caused.
        /// </summary>
        public InstanceID? EffectOrigin { get; } = RequireEffectOrigin(FromPrimaryOperation, EffectOrigin);

        /// <summary>
        /// The offering branch's effect-origin lineage. Validated non-null at construction.
        /// </summary>
        public EffectOriginLineage BranchLineage { get; } = RequireLineage(BranchLineage);

        /// <summary>
        /// The lockable player Instruction slot. Validated at construction: non-null exactly when the
        /// change is a player-owned primary operation.
        /// </summary>
        public SourceSlot HostSlot { get; } = RequireHostSlot(HostSlot, FromPrimaryOperation, Ownership);

        /// <summary>
        /// The host slot's Structure context. Validated at construction: non-null only when the host
        /// slot is non-null.
        /// </summary>
        public StructureContext SlotContext { get; } = RequireSlotContext(SlotContext, HostSlot);

        /// <summary>
        /// Validates that a unit-less change carries the boundary-caused shape.
        /// </summary>
        /// <param name="unit">The candidate containing unit.</param>
        /// <param name="fromPrimaryOperation">Whether the change is primary-operation-caused.</param>
        /// <param name="effectOrigin">The candidate effect origin.</param>
        /// <returns>The unit unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when a unit-less change is not effect-caused.</exception>
        private static RuntimeUnitID? RequireUnit(
            RuntimeUnitID? unit,
            bool fromPrimaryOperation,
            InstanceID? effectOrigin
        )
        {
            if (unit == null && (fromPrimaryOperation || effectOrigin == null))
                throw new ArgumentException("Only a boundary-caused change resolves outside a runtime unit.", nameof(unit));

            return unit;
        }

        /// <summary>
        /// Validates that the causal depth is non-negative.
        /// </summary>
        /// <param name="causalDepth">The candidate depth.</param>
        /// <returns>The depth unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the depth is negative.</exception>
        private static int RequireDepth(int causalDepth)
        {
            if (causalDepth < 0)
                throw new ArgumentException("An occurrence requires a non-negative causal depth.", nameof(causalDepth));

            return causalDepth;
        }

        /// <summary>
        /// Validates that a primary-operation change carries its source ownership.
        /// </summary>
        /// <param name="fromPrimaryOperation">Whether the change is primary-operation-caused.</param>
        /// <param name="ownership">The candidate ownership.</param>
        /// <returns>The ownership unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when a primary change carries no ownership.</exception>
        private static OwnershipClassification? RequireOwnership(
            bool fromPrimaryOperation,
            OwnershipClassification? ownership
        )
        {
            if (fromPrimaryOperation && ownership == null)
                throw new ArgumentException("A primary-operation change requires its source ownership.", nameof(ownership));

            return ownership;
        }

        /// <summary>
        /// Validates that the effect origin is present exactly when the change is effect-caused.
        /// </summary>
        /// <param name="fromPrimaryOperation">Whether the change is primary-operation-caused.</param>
        /// <param name="effectOrigin">The candidate effect origin.</param>
        /// <returns>The effect origin unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when presence and origin kind disagree.</exception>
        private static InstanceID? RequireEffectOrigin(
            bool fromPrimaryOperation,
            InstanceID? effectOrigin
        )
        {
            if (!fromPrimaryOperation && effectOrigin == null)
                throw new ArgumentException("An effect-caused change requires its effect origin.", nameof(effectOrigin));

            if (fromPrimaryOperation && effectOrigin != null)
                throw new ArgumentException("A primary-operation change carries no effect origin.", nameof(effectOrigin));

            return effectOrigin;
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

        /// <summary>
        /// Validates that the host slot is present exactly when the change is a player-owned primary
        /// operation — the only lockable source execution (CAB-EVT-435 excludes reactions, boundary
        /// effects, and Core Instructions).
        /// </summary>
        /// <param name="hostSlot">The candidate host slot.</param>
        /// <param name="fromPrimaryOperation">Whether the change is primary-operation-caused.</param>
        /// <param name="ownership">The source ownership.</param>
        /// <returns>The host slot unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when presence and lockability disagree.</exception>
        private static SourceSlot RequireHostSlot(
            SourceSlot hostSlot,
            bool fromPrimaryOperation,
            OwnershipClassification? ownership
        )
        {
            bool lockable = fromPrimaryOperation && ownership == OwnershipClassification.PlayerOwned;
            if (lockable && hostSlot == null)
                throw new ArgumentException("A player-owned primary-operation change requires its host slot.", nameof(hostSlot));

            if (!lockable && hostSlot != null)
                throw new ArgumentException("Only a player-owned primary-operation change carries a host slot.", nameof(hostSlot));

            return hostSlot;
        }

        /// <summary>
        /// Validates that a slot context is present only alongside a host slot. The host slot's own
        /// pairing has already enforced the player-primary rule, so this guard only rejects a context
        /// with no slot.
        /// </summary>
        /// <param name="slotContext">The candidate slot context.</param>
        /// <param name="hostSlot">The validated host slot.</param>
        /// <returns>The slot context unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when a slot context accompanies no host slot.</exception>
        private static StructureContext RequireSlotContext(StructureContext slotContext, SourceSlot hostSlot)
        {
            if (slotContext != null && hostSlot == null)
                throw new ArgumentException("A slot context requires a host slot.", nameof(slotContext));

            return slotContext;
        }
    }
}