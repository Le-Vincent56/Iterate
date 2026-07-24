using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests <see cref="AddedExecutionRequest"/>'s construction contract — the CAB-required field
    /// validation, the lineage-depth/proposed-depth invariant, and the null-while-pending parent —
    /// plus <see cref="AddedExecutionRequestIdentities"/>'s deterministic identity format.
    /// </summary>
    public sealed class AddedExecutionRequestTests
    {
        private static readonly InstructionDefinition _instructionDefinition = new(
            new InstructionID("WB-INS-002"),
            "rules",
            "Add 2 to Value",
            ContentCategory.Instruction,
            Rarity.Common,
            Array.Empty<string>(),
            1,
            null,
            null,
            Array.Empty<string>());

        [Test]
        public void Constructor_RoundTrips()
        {
            SourceSlot slot = InstructionSlot();
            EffectOriginLineage lineage = EffectOriginLineage.Empty.Append(new InstanceID(9));

            AddedExecutionRequest request = new AddedExecutionRequest(
                "WB-DIR-001:0#9@e7",
                new InstanceID(9),
                "WB-DIR-001",
                new TraceEventID(7),
                null,
                slot,
                null,
                lineage,
                1,
                new RuntimeUnitID(3));

            Assert.AreEqual("WB-DIR-001:0#9@e7", request.RequestIdentity);
            Assert.AreEqual(new InstanceID(9), request.CreatorOrigin);
            Assert.AreEqual("WB-DIR-001", request.CreatorDefinitionID);
            Assert.AreEqual(new TraceEventID(7), request.TriggeringEvent);
            Assert.IsNull(request.RequestedEvent);
            Assert.AreSame(slot, request.LockedSlot);
            Assert.IsNull(request.InheritedContext);
            Assert.AreSame(lineage, request.Lineage);
            Assert.AreEqual(1, request.ProposedDepth);
            Assert.AreEqual(new RuntimeUnitID(3), request.ParentUnit);
        }

        [Test]
        public void Constructor_EmptyIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Request(
                identity: string.Empty));
        }

        [Test]
        public void Constructor_EmptyCreatorDefinitionID_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Request(
                creatorDefinitionID: string.Empty));
        }

        [Test]
        public void Constructor_NullSlot_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Request(
                slot: null, useDefaultSlot: false));
        }

        [Test]
        public void Constructor_CoreSlot_Throws()
        {
            CoreLine core = new("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));

            Assert.Throws<ArgumentException>(() => _ = Request(
                slot: SourceSlot.ForCore(new SourcePosition(1), core), useDefaultSlot: false));
        }

        [Test]
        public void Constructor_StructureHeaderSlot_Throws()
        {
            StructureDefinition repeat = new(
                new StructureID("WB-STR-001"),
                "Repeat 2",
                "Repeat 2",
                ContentCategory.Structure,
                Rarity.Common,
                Array.Empty<string>(),
                3,
                StructureKind.Repeat,
                2,
                null);
            StructureInstance structure = new(new InstanceID(4), repeat);

            Assert.Throws<ArgumentException>(() => _ = Request(
                slot: SourceSlot.ForStructureHeader(new SourcePosition(2), structure), useDefaultSlot: false));
        }

        [Test]
        public void Constructor_NullLineage_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Request(
                lineage: null, useDefaultLineage: false));
        }

        [Test]
        public void Constructor_EmptyLineage_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Request(
                lineage: EffectOriginLineage.Empty, useDefaultLineage: false));
        }

        [Test]
        public void Constructor_DepthZero_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Request(
                proposedDepth: 0));
        }

        [Test]
        public void Constructor_DepthMismatch_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Request(
                proposedDepth: 2));
        }

        [Test]
        public void Constructor_NullParentUnit_Accepted()
        {
            AddedExecutionRequest request = Request(parentUnit: null);

            Assert.IsNull(request.ParentUnit);
        }

        [Test]
        public void With_ParentUnit_Completes()
        {
            AddedExecutionRequest pending = Request(parentUnit: null);

            AddedExecutionRequest completed = pending with { ParentUnit = new RuntimeUnitID(6) };

            Assert.AreEqual(new RuntimeUnitID(6), completed.ParentUnit);
            Assert.IsNull(pending.ParentUnit);
        }

        [Test]
        public void With_RequestedEvent_Completes()
        {
            AddedExecutionRequest built = Request(parentUnit: new RuntimeUnitID(3));

            AddedExecutionRequest completed = built with { RequestedEvent = new TraceEventID(11) };

            Assert.AreEqual(new TraceEventID(11), completed.RequestedEvent);
            Assert.IsNull(built.RequestedEvent);
        }

        [Test]
        public void Identities_For_ComposesFrequencyKeyAndEvent()
        {
            ActiveEffect creator = ActiveEffect.ForAddedExecution(
                new InstanceID(7),
                "WB-DEP-009",
                0,
                PostUnitTrigger(),
                new AddedExecutionRequestOperation(new TargetingRule("TRIGGERING_UNIT", string.Empty), false),
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "EXECUTION"));

            string identity = AddedExecutionRequestIdentities.For(creator, new TraceEventID(42));

            Assert.AreEqual("WB-DEP-009:0#7@e42", identity);
        }

        [Test]
        public void Identities_For_NullCreator_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = AddedExecutionRequestIdentities.For(null, new TraceEventID(1)));
        }

        /// <summary>
        /// Builds a valid request with the given component overridden.
        /// </summary>
        /// <param name="identity">The request identity.</param>
        /// <param name="creatorDefinitionID">The creator's definition identity.</param>
        /// <param name="slot">The locked slot when not using the default.</param>
        /// <param name="useDefaultSlot">Whether to substitute the standard Instruction slot.</param>
        /// <param name="lineage">The lineage when not using the default.</param>
        /// <param name="useDefaultLineage">Whether to substitute the standard one-entry lineage.</param>
        /// <param name="proposedDepth">The proposed added-execution depth.</param>
        /// <param name="parentUnit">The parent unit, or null while pending.</param>
        /// <returns>The constructed request.</returns>
        private static AddedExecutionRequest Request(
            string identity = "WB-DIR-001:0#9@e7",
            string creatorDefinitionID = "WB-DIR-001",
            SourceSlot slot = null,
            bool useDefaultSlot = true,
            EffectOriginLineage lineage = null,
            bool useDefaultLineage = true,
            int proposedDepth = 1,
            RuntimeUnitID? parentUnit = null)
        {
            if (useDefaultSlot && slot == null)
                slot = InstructionSlot();
            if (useDefaultLineage && lineage == null)
                lineage = EffectOriginLineage.Empty.Append(new InstanceID(9));

            return new AddedExecutionRequest(
                identity,
                new InstanceID(9),
                creatorDefinitionID,
                new TraceEventID(7),
                null,
                slot,
                null,
                lineage,
                proposedDepth,
                parentUnit);
        }

        /// <summary>
        /// The standard top-level Instruction slot fixture.
        /// </summary>
        /// <returns>The slot.</returns>
        private static SourceSlot InstructionSlot()
        {
            InstructionInstance instruction = new(new InstanceID(1), _instructionDefinition, null);
            return SourceSlot.ForInstruction(new SourcePosition(2), instruction);
        }

        /// <summary>
        /// The post-unit trigger pair carrying the Repeat-context qualifier.
        /// </summary>
        /// <returns>The trigger descriptor.</returns>
        private static TriggerDescriptor PostUnitTrigger()
        {
            return new TriggerDescriptor(
                EventFamily.Lifecycle,
                "RUNTIME_UNIT_COMPLETED",
                new List<TriggerQualifier> { new TriggerQualifier("STRUCTURE_CONTEXT", "INSIDE_REPEAT") },
                new EffectTiming(TimingKind.Band, "POST_UNIT_CONSEQUENCE_AND_EVIDENCE"));
        }
    }
}
