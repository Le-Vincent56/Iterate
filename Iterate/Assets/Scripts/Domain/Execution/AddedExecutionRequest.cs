using System;
using Iterate.Domain.Compilation;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One committed added-execution request: the deterministic request identity, the creator effect
    /// origin and its definition key, the triggering and request evidence events, the locked host
    /// slot, the inherited Structure context, the branch lineage already extended with the creator,
    /// the proposed added-execution depth, and the parent unit — null only while the request is
    /// pending. The lineage depth and proposed depth encode one fact and must agree.
    /// </summary>
    /// <param name="RequestIdentity">The deterministic request identity; non-empty.</param>
    /// <param name="CreatorOrigin">The creating effect instance's identity.</param>
    /// <param name="CreatorDefinitionID">The creator's definition identity; non-empty.</param>
    /// <param name="TriggeringEvent">The event whose qualification created the request.</param>
    /// <param name="RequestedEvent">The request's own evidence event; null until emitted.</param>
    /// <param name="LockedSlot">The locked host slot; an Instruction or contained Instruction.</param>
    /// <param name="InheritedContext">The inherited Structure context; null for a top-level host.</param>
    /// <param name="Lineage">The branch lineage including the creator; depth one or greater.</param>
    /// <param name="ProposedDepth">The descendant's added-execution depth; equals the lineage depth.</param>
    /// <param name="ParentUnit">The causing unit; null only while the request is pending.</param>
    public sealed record AddedExecutionRequest(
        string RequestIdentity,
        InstanceID CreatorOrigin,
        string CreatorDefinitionID,
        TraceEventID TriggeringEvent,
        TraceEventID? RequestedEvent,
        SourceSlot LockedSlot,
        StructureContext InheritedContext,
        EffectOriginLineage Lineage,
        int ProposedDepth,
        RuntimeUnitID? ParentUnit
    )
    {
        /// <summary>
        /// The deterministic request identity. Validated non-empty at construction.
        /// </summary>
        public string RequestIdentity { get; } = RequireIdentity(RequestIdentity);

        /// <summary>
        /// The creator's definition identity. Validated non-empty at construction.
        /// </summary>
        public string CreatorDefinitionID { get; } = RequireDefinitionID(CreatorDefinitionID);

        /// <summary>
        /// The locked host slot. Validated at construction: only an ordinary or contained
        /// Instruction is an eligible added-execution host.
        /// </summary>
        public SourceSlot LockedSlot { get; } = RequireEligibleHost(LockedSlot);

        /// <summary>
        /// The branch lineage including the creator. Validated at construction: non-null, at least
        /// one entry, and its depth equals the proposed depth.
        /// </summary>
        public EffectOriginLineage Lineage { get; } = RequireLineage(Lineage, ProposedDepth);

        /// <summary>
        /// Validates that the request identity is present.
        /// </summary>
        /// <param name="requestIdentity">The candidate identity.</param>
        /// <returns>The identity unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the identity is null or empty.</exception>
        private static string RequireIdentity(string requestIdentity)
        {
            if (string.IsNullOrEmpty(requestIdentity))
                throw new ArgumentException("An AddedExecutionRequest requires a request identity.", nameof(requestIdentity));

            return requestIdentity;
        }

        /// <summary>
        /// Validates that the creator's definition identity is present.
        /// </summary>
        /// <param name="creatorDefinitionID">The candidate definition identity.</param>
        /// <returns>The identity unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the identity is null or empty.</exception>
        private static string RequireDefinitionID(string creatorDefinitionID)
        {
            if (string.IsNullOrEmpty(creatorDefinitionID))
                throw new ArgumentException("An AddedExecutionRequest requires a creator definition identity.", nameof(creatorDefinitionID));

            return creatorDefinitionID;
        }

        /// <summary>
        /// Validates that the locked slot is an eligible added-execution host: an ordinary or
        /// contained Instruction.
        /// </summary>
        /// <param name="lockedSlot">The candidate slot.</param>
        /// <returns>The slot unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the slot is null or not an Instruction host.</exception>
        private static SourceSlot RequireEligibleHost(SourceSlot lockedSlot)
        {
            if (lockedSlot == null)
                throw new ArgumentException("An AddedExecutionRequest requires a locked host slot.", nameof(lockedSlot));

            if (lockedSlot.Kind != SourceSlotKind.Instruction && lockedSlot.Kind != SourceSlotKind.ContainedInstruction)
                throw new ArgumentException($"An AddedExecutionRequest cannot lock a '{lockedSlot.Kind}' slot; only an Instruction host is eligible.", nameof(lockedSlot));

            return lockedSlot;
        }

        /// <summary>
        /// Validates that the lineage is present with at least its creator and that its depth equals
        /// the proposed added-execution depth.
        /// </summary>
        /// <param name="lineage">The candidate lineage.</param>
        /// <param name="proposedDepth">The proposed added-execution depth.</param>
        /// <returns>The lineage unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the lineage is null, empty, or disagrees with the depth.</exception>
        private static EffectOriginLineage RequireLineage(EffectOriginLineage lineage, int proposedDepth)
        {
            if (lineage == null)
                throw new ArgumentException("An AddedExecutionRequest requires a lineage.", nameof(lineage));

            if (lineage.Depth < 1)
                throw new ArgumentException("An AddedExecutionRequest's lineage contains at least its creator.", nameof(lineage));

            if (proposedDepth < 1)
                throw new ArgumentException("An AddedExecutionRequest requires a proposed depth of at least one.", nameof(proposedDepth));

            if (lineage.Depth != proposedDepth)
                throw new ArgumentException($"The lineage depth {lineage.Depth} and proposed depth {proposedDepth} encode one fact and must agree.", nameof(proposedDepth));

            return lineage;
        }
    }
}