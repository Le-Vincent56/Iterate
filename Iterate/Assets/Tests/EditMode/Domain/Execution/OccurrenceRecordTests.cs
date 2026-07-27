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
    /// Tests the typed occurrence records the effect engine matches against:
    /// <see cref="OperationOccurrence"/>'s operand-source and ownership/host pairing rules,
    /// <see cref="QuantityOccurrence"/>'s origin rules — a primary change carries its source
    /// ownership, a reaction-caused change carries its effect origin, never both ways — plus its
    /// host-slot pairing (a lockable host slot exactly when the change is a primary-operation source
    /// execution), <see cref="PostUnitOccurrence"/>'s four Patch facts, and
    /// <see cref="SkipOccurrence"/>'s depth, cause, and ownership/host validation.
    /// </summary>
    public sealed class OccurrenceRecordTests
    {
        [Test]
        public void OperationOccurrence_RegisterOperand_RoundTrips()
        {
            OperationOccurrence occurrence = new OperationOccurrence(
                new RuntimeUnitID(3),
                new TraceEventID(11),
                0,
                new InstanceID(7),
                CoreRegister.Value,
                CoreLineOperator.Add,
                OperandSource.Register,
                CoreRegister.Signal,
                OwnershipClassification.PlayerOwned);

            Assert.AreEqual(new RuntimeUnitID(3), occurrence.Unit);
            Assert.AreEqual(new TraceEventID(11), occurrence.Event);
            Assert.AreEqual(0, occurrence.CausalDepth);
            Assert.AreEqual(new InstanceID(7), occurrence.HostInstance);
            Assert.AreEqual(CoreRegister.Value, occurrence.Register);
            Assert.AreEqual(CoreLineOperator.Add, occurrence.Operator);
            Assert.AreEqual(OperandSource.Register, occurrence.OperandSource);
            Assert.AreEqual(CoreRegister.Signal, occurrence.OperandRegister);
            Assert.AreEqual(OwnershipClassification.PlayerOwned, occurrence.Ownership);
        }

        [Test]
        public void OperationOccurrence_CoreOwned_CarriesNullHost()
        {
            OperationOccurrence occurrence = new OperationOccurrence(
                new RuntimeUnitID(2),
                new TraceEventID(9),
                0,
                null,
                CoreRegister.Value,
                CoreLineOperator.Assign,
                OperandSource.Constant,
                null,
                OwnershipClassification.CoreOwned);

            Assert.IsNull(occurrence.HostInstance);
            Assert.AreEqual(OwnershipClassification.CoreOwned, occurrence.Ownership);
        }

        [Test]
        public void OperationOccurrence_NegativeDepth_Throws()
        {
            Assert.Throws<ArgumentException>(() => new OperationOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                -1,
                new InstanceID(7),
                CoreRegister.Value,
                CoreLineOperator.Add,
                OperandSource.Constant,
                null,
                OwnershipClassification.PlayerOwned));
        }

        [Test]
        public void OperationOccurrence_RegisterOperandWithoutOperandRegister_Throws()
        {
            Assert.Throws<ArgumentException>(() => new OperationOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                0,
                new InstanceID(7),
                CoreRegister.Value,
                CoreLineOperator.Add,
                OperandSource.Register,
                null,
                OwnershipClassification.PlayerOwned));
        }

        [Test]
        public void OperationOccurrence_ConstantOperandWithOperandRegister_Throws()
        {
            Assert.Throws<ArgumentException>(() => new OperationOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                0,
                new InstanceID(7),
                CoreRegister.Value,
                CoreLineOperator.Add,
                OperandSource.Constant,
                CoreRegister.Signal,
                OwnershipClassification.PlayerOwned));
        }

        [Test]
        public void OperationOccurrence_PlayerOwnedWithoutHost_Throws()
        {
            Assert.Throws<ArgumentException>(() => new OperationOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                0,
                null,
                CoreRegister.Value,
                CoreLineOperator.Add,
                OperandSource.Constant,
                null,
                OwnershipClassification.PlayerOwned));
        }

        [Test]
        public void OperationOccurrence_CoreOwnedWithHost_Throws()
        {
            Assert.Throws<ArgumentException>(() => new OperationOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                0,
                new InstanceID(7),
                CoreRegister.Value,
                CoreLineOperator.Assign,
                OperandSource.Constant,
                null,
                OwnershipClassification.CoreOwned));
        }

        [Test]
        public void SkipOccurrence_PlayerOwned_RoundTrips()
        {
            SkipOccurrence occurrence = new SkipOccurrence(
                new RuntimeUnitID(4),
                new TraceEventID(18),
                1,
                OwnershipClassification.PlayerOwned,
                new InstanceID(9),
                "CONDITION_FALSE:#5@3#1/eval-1",
                true);

            Assert.AreEqual(new RuntimeUnitID(4), occurrence.Unit);
            Assert.AreEqual(new TraceEventID(18), occurrence.SkipEvent);
            Assert.AreEqual(1, occurrence.CausalDepth);
            Assert.AreEqual(OwnershipClassification.PlayerOwned, occurrence.Ownership);
            Assert.AreEqual(new InstanceID(9), occurrence.HostInstance);
            Assert.AreEqual("CONDITION_FALSE:#5@3#1/eval-1", occurrence.SkipCause);
            Assert.IsTrue(occurrence.Rescuable);
        }

        [Test]
        public void SkipOccurrence_NegativeDepth_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SkipOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                -1,
                OwnershipClassification.PlayerOwned,
                new InstanceID(9),
                "CONDITION_FALSE:#5@3#1/eval-1",
                true));
        }

        [Test]
        public void SkipOccurrence_EmptyCause_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SkipOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                1,
                OwnershipClassification.PlayerOwned,
                new InstanceID(9),
                string.Empty,
                true));
        }

        [Test]
        public void SkipOccurrence_PlayerOwnedWithoutHost_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SkipOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                1,
                OwnershipClassification.PlayerOwned,
                null,
                "CONDITION_FALSE:#5@3#1/eval-1",
                true));
        }

        [Test]
        public void SkipOccurrence_CoreOwnedWithHost_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SkipOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                1,
                OwnershipClassification.CoreOwned,
                new InstanceID(9),
                "CONDITION_FALSE:#5@3#1/eval-1",
                true));
        }

        [Test]
        public void QuantityOccurrence_PrimaryChange_RoundTrips()
        {
            StructureContext slotContext = RepeatContext();
            SourceSlot hostSlot = InstructionSlot(60);

            QuantityOccurrence occurrence = new QuantityOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(21),
                0,
                CoreRegister.Score,
                4,
                OwnershipClassification.PlayerOwned,
                null,
                true,
                EffectOriginLineage.Empty,
                hostSlot,
                slotContext);

            Assert.AreEqual(new RuntimeUnitID(5), occurrence.Unit);
            Assert.AreEqual(new TraceEventID(21), occurrence.Event);
            Assert.AreEqual(0, occurrence.CausalDepth);
            Assert.AreEqual(CoreRegister.Score, occurrence.Register);
            Assert.AreEqual(4, occurrence.ActualDelta);
            Assert.AreEqual(OwnershipClassification.PlayerOwned, occurrence.Ownership);
            Assert.IsNull(occurrence.EffectOrigin);
            Assert.IsTrue(occurrence.FromPrimaryOperation);
            Assert.AreSame(hostSlot, occurrence.HostSlot);
            Assert.AreSame(slotContext, occurrence.SlotContext);
        }

        [Test]
        public void QuantityOccurrence_ReactionChange_RoundTrips()
        {
            QuantityOccurrence occurrence = new QuantityOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(30),
                2,
                CoreRegister.Value,
                1,
                null,
                new InstanceID(9),
                false,
                EffectOriginLineage.Empty,
                null,
                null);

            Assert.AreEqual(2, occurrence.CausalDepth);
            Assert.IsNull(occurrence.Ownership);
            Assert.AreEqual(new InstanceID(9), occurrence.EffectOrigin);
            Assert.IsFalse(occurrence.FromPrimaryOperation);
            Assert.IsNull(occurrence.HostSlot);
            Assert.IsNull(occurrence.SlotContext);
        }

        [Test]
        public void QuantityOccurrence_PrimaryTopLevel_AcceptsNullSlotContext()
        {
            QuantityOccurrence occurrence = new QuantityOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(21),
                0,
                CoreRegister.Score,
                4,
                OwnershipClassification.PlayerOwned,
                null,
                true,
                EffectOriginLineage.Empty,
                InstructionSlot(60),
                null);

            Assert.IsNotNull(occurrence.HostSlot);
            Assert.IsNull(occurrence.SlotContext);
        }

        [Test]
        public void QuantityOccurrence_PrimaryWithoutHostSlot_Throws()
        {
            Assert.Throws<ArgumentException>(() => new QuantityOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(21),
                0,
                CoreRegister.Score,
                4,
                OwnershipClassification.PlayerOwned,
                null,
                true,
                EffectOriginLineage.Empty,
                null,
                null));
        }

        [Test]
        public void QuantityOccurrence_CorePrimaryChange_RequiresNullHostSlot()
        {
            QuantityOccurrence occurrence = new QuantityOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(21),
                0,
                CoreRegister.Score,
                4,
                OwnershipClassification.CoreOwned,
                null,
                true,
                EffectOriginLineage.Empty,
                null,
                null);

            Assert.IsNull(occurrence.HostSlot);
            Assert.IsTrue(occurrence.FromPrimaryOperation);
        }

        [Test]
        public void QuantityOccurrence_CorePrimaryWithHostSlot_Throws()
        {
            Assert.Throws<ArgumentException>(() => new QuantityOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(21),
                0,
                CoreRegister.Score,
                4,
                OwnershipClassification.CoreOwned,
                null,
                true,
                EffectOriginLineage.Empty,
                InstructionSlot(60),
                null));
        }

        [Test]
        public void QuantityOccurrence_ReactionWithHostSlot_Throws()
        {
            Assert.Throws<ArgumentException>(() => new QuantityOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(30),
                2,
                CoreRegister.Value,
                1,
                null,
                new InstanceID(9),
                false,
                EffectOriginLineage.Empty,
                InstructionSlot(60),
                null));
        }

        [Test]
        public void QuantityOccurrence_SlotContextWithoutHostSlot_Throws()
        {
            Assert.Throws<ArgumentException>(() => new QuantityOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(30),
                2,
                CoreRegister.Value,
                1,
                null,
                new InstanceID(9),
                false,
                EffectOriginLineage.Empty,
                null,
                RepeatContext()));
        }

        [Test]
        public void QuantityOccurrence_NegativeDepth_Throws()
        {
            Assert.Throws<ArgumentException>(() => new QuantityOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                -1,
                CoreRegister.Value,
                1,
                OwnershipClassification.PlayerOwned,
                null,
                true,
                EffectOriginLineage.Empty,
                InstructionSlot(60),
                null));
        }

        [Test]
        public void QuantityOccurrence_PrimaryWithoutOwnership_Throws()
        {
            Assert.Throws<ArgumentException>(() => new QuantityOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                0,
                CoreRegister.Value,
                1,
                null,
                null,
                true,
                EffectOriginLineage.Empty,
                InstructionSlot(60),
                null));
        }

        [Test]
        public void QuantityOccurrence_PrimaryWithEffectOrigin_Throws()
        {
            Assert.Throws<ArgumentException>(() => new QuantityOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                0,
                CoreRegister.Value,
                1,
                OwnershipClassification.PlayerOwned,
                new InstanceID(9),
                true,
                EffectOriginLineage.Empty,
                InstructionSlot(60),
                null));
        }

        [Test]
        public void QuantityOccurrence_ReactionWithoutEffectOrigin_Throws()
        {
            Assert.Throws<ArgumentException>(() => new QuantityOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                1,
                CoreRegister.Value,
                1,
                null,
                null,
                false,
                EffectOriginLineage.Empty,
                null,
                null));
        }

        [Test]
        public void QuantityOccurrence_NullLineage_Throws()
        {
            Assert.Throws<ArgumentException>(() => new QuantityOccurrence(
                new RuntimeUnitID(1),
                new TraceEventID(1),
                0,
                CoreRegister.Value,
                1,
                OwnershipClassification.PlayerOwned,
                null,
                true,
                null,
                InstructionSlot(60),
                null));
        }

        [Test]
        public void QuantityOccurrence_BoundaryCausedChange_AcceptsNullUnit()
        {
            QuantityOccurrence occurrence = new QuantityOccurrence(
                null,
                new TraceEventID(40),
                2,
                CoreRegister.Value,
                1,
                null,
                new InstanceID(9),
                false,
                EffectOriginLineage.Empty,
                null,
                null);

            Assert.IsNull(occurrence.Unit);
            Assert.AreEqual(new InstanceID(9), occurrence.EffectOrigin);
            Assert.AreEqual(EffectOriginLineage.Empty, occurrence.BranchLineage);
            Assert.IsNull(occurrence.HostSlot);
        }

        [Test]
        public void QuantityOccurrence_NullUnitOnPrimaryChange_Throws()
        {
            Assert.Throws<ArgumentException>(() => new QuantityOccurrence(
                null,
                new TraceEventID(40),
                0,
                CoreRegister.Value,
                1,
                OwnershipClassification.PlayerOwned,
                null,
                true,
                EffectOriginLineage.Empty,
                InstructionSlot(60),
                null));
        }

        [Test]
        public void QuantityOccurrence_BranchLineage_RoundTrips()
        {
            EffectOriginLineage lineage = EffectOriginLineage.Empty.Append(new InstanceID(7));

            QuantityOccurrence occurrence = new QuantityOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(21),
                0,
                CoreRegister.Value,
                2,
                OwnershipClassification.PlayerOwned,
                null,
                true,
                lineage,
                InstructionSlot(60),
                null);

            Assert.AreSame(lineage, occurrence.BranchLineage);
        }

        [Test]
        public void PostUnitOccurrence_RoundTrips()
        {
            StructureContext context = RepeatContext();

            PostUnitOccurrence occurrence = new PostUnitOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(30),
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                new InstanceID(50),
                context,
                EffectOriginLineage.Empty,
                new SourcePosition(2),
                false,
                null,
                false);

            Assert.AreEqual(new RuntimeUnitID(5), occurrence.Unit);
            Assert.AreEqual(new TraceEventID(30), occurrence.CompletionEvent);
            Assert.AreEqual(EventDisposition.Resolved, occurrence.FinalDisposition);
            Assert.AreEqual(OwnershipClassification.PlayerOwned, occurrence.Ownership);
            Assert.AreEqual(new InstanceID(50), occurrence.HostInstance);
            Assert.AreSame(context, occurrence.StructureContext);
            Assert.AreEqual(EffectOriginLineage.Empty, occurrence.BranchLineage);
        }

        [Test]
        public void PostUnitOccurrence_Facts_RoundTrip()
        {
            PostUnitOccurrence occurrence = new PostUnitOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(30),
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                new InstanceID(50),
                null,
                EffectOriginLineage.Empty,
                new SourcePosition(4),
                true,
                ConditionOutcome.True,
                true);

            Assert.AreEqual(new SourcePosition(4), occurrence.Position);
            Assert.IsTrue(occurrence.IsFinalOccupiedPlayerLine);
            Assert.AreEqual(ConditionOutcome.True, occurrence.ConditionResult);
            Assert.IsTrue(occurrence.AdjacentAfterSuccessfulScore);
        }

        [Test]
        public void PostUnitOccurrence_RescuedFromFalse_CarriesFalseConditionResult()
        {
            PostUnitOccurrence occurrence = new PostUnitOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(30),
                EventDisposition.Rescued,
                OwnershipClassification.PlayerOwned,
                new InstanceID(50),
                null,
                EffectOriginLineage.Empty,
                new SourcePosition(4),
                false,
                ConditionOutcome.False,
                false);

            Assert.AreEqual(ConditionOutcome.False, occurrence.ConditionResult);
        }

        [Test]
        public void PostUnitOccurrence_CoreOwnedFinalPlayerLine_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PostUnitOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(30),
                EventDisposition.Resolved,
                OwnershipClassification.CoreOwned,
                null,
                null,
                EffectOriginLineage.Empty,
                new SourcePosition(4),
                true,
                null,
                false));
        }

        [Test]
        public void PostUnitOccurrence_NullStructureContext_Accepted()
        {
            PostUnitOccurrence occurrence = new PostUnitOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(30),
                EventDisposition.Resolved,
                OwnershipClassification.CoreOwned,
                null,
                null,
                EffectOriginLineage.Empty,
                new SourcePosition(1),
                false,
                null,
                false);

            Assert.IsNull(occurrence.StructureContext);
        }

        [Test]
        public void PostUnitOccurrence_PlayerOwnedWithoutHost_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PostUnitOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(30),
                EventDisposition.Resolved,
                OwnershipClassification.PlayerOwned,
                null,
                null,
                EffectOriginLineage.Empty,
                new SourcePosition(2),
                false,
                null,
                false));
        }

        [Test]
        public void PostUnitOccurrence_CoreOwnedWithHost_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PostUnitOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(30),
                EventDisposition.Resolved,
                OwnershipClassification.CoreOwned,
                new InstanceID(50),
                null,
                EffectOriginLineage.Empty,
                new SourcePosition(2),
                false,
                null,
                false));
        }

        [Test]
        public void PostUnitOccurrence_NullLineage_Throws()
        {
            Assert.Throws<ArgumentException>(() => new PostUnitOccurrence(
                new RuntimeUnitID(5),
                new TraceEventID(30),
                EventDisposition.Resolved,
                OwnershipClassification.CoreOwned,
                null,
                null,
                null,
                new SourcePosition(2),
                false,
                null,
                false));
        }

        [Test]
        public void ConditionSuccessOccurrence_RoundTrips()
        {
            StructureContext context = EvaluationContext();

            ConditionSuccessOccurrence occurrence = new ConditionSuccessOccurrence(
                new TraceEventID(33),
                "#5@3#1/eval-1",
                new InstanceID(60),
                new SourcePosition(4),
                context,
                EffectOriginLineage.Empty);

            Assert.AreEqual(new TraceEventID(33), occurrence.ResultEvent);
            Assert.AreEqual("#5@3#1/eval-1", occurrence.EvaluationIdentity);
            Assert.AreEqual(new InstanceID(60), occurrence.FirstOccupiedChild);
            Assert.AreEqual(new SourcePosition(4), occurrence.FirstOccupiedChildPosition);
            Assert.AreSame(context, occurrence.EvaluationContext);
        }

        [Test]
        public void ConditionSuccessOccurrence_NoOccupiedChild_RoundTrips()
        {
            ConditionSuccessOccurrence occurrence = new ConditionSuccessOccurrence(
                new TraceEventID(33),
                "#5@3#1/eval-1",
                null,
                null,
                EvaluationContext(),
                EffectOriginLineage.Empty);

            Assert.IsNull(occurrence.FirstOccupiedChild);
            Assert.IsNull(occurrence.FirstOccupiedChildPosition);
        }

        [Test]
        public void ConditionSuccessOccurrence_EmptyIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => new ConditionSuccessOccurrence(
                new TraceEventID(33),
                string.Empty,
                null,
                null,
                EvaluationContext(),
                EffectOriginLineage.Empty));
        }

        [Test]
        public void ConditionSuccessOccurrence_ChildWithoutPosition_Throws()
        {
            Assert.Throws<ArgumentException>(() => new ConditionSuccessOccurrence(
                new TraceEventID(33),
                "#5@3#1/eval-1",
                new InstanceID(60),
                null,
                EvaluationContext(),
                EffectOriginLineage.Empty));
        }

        [Test]
        public void ConditionSuccessOccurrence_PositionWithoutChild_Throws()
        {
            Assert.Throws<ArgumentException>(() => new ConditionSuccessOccurrence(
                new TraceEventID(33),
                "#5@3#1/eval-1",
                null,
                new SourcePosition(4),
                EvaluationContext(),
                EffectOriginLineage.Empty));
        }

        [Test]
        public void ConditionSuccessOccurrence_NullContext_Throws()
        {
            Assert.Throws<ArgumentException>(() => new ConditionSuccessOccurrence(
                new TraceEventID(33),
                "#5@3#1/eval-1",
                null,
                null,
                null,
                EffectOriginLineage.Empty));
        }

        [Test]
        public void BoundaryOccurrence_RoundTrips()
        {
            BoundaryOccurrence occurrence = new BoundaryOccurrence(
                "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL",
                7,
                3,
                12);

            Assert.AreEqual("END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL", occurrence.BoundaryName);
            Assert.AreEqual(7, occurrence.Value);
            Assert.AreEqual(3, occurrence.Signal);
            Assert.AreEqual(12, occurrence.Score);
        }

        [Test]
        public void BoundaryOccurrence_EmptyName_Throws()
        {
            Assert.Throws<ArgumentException>(() => new BoundaryOccurrence(string.Empty, 0, 0, 0));
        }

        /// <summary>
        /// Builds a top-level Instruction slot carrying an instance with the given identity, for the
        /// occurrences whose host-slot field records a lockable source execution.
        /// </summary>
        /// <param name="instanceID">The occupying instance's identity value.</param>
        /// <returns>The Instruction slot.</returns>
        private static SourceSlot InstructionSlot(int instanceID)
        {
            InstructionDefinition definition = new InstructionDefinition(
                new InstructionID("WB-INS-001"),
                "rules",
                "TEST INSTRUCTION",
                ContentCategory.Instruction,
                Rarity.Starter,
                new List<string>(),
                1,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                null,
                new List<string>());

            return SourceSlot.ForInstruction(
                new SourcePosition(2),
                new InstructionInstance(new InstanceID(instanceID), definition, null));
        }

        /// <summary>
        /// A Structure context carrying an active Repeat-iteration identity.
        /// </summary>
        /// <returns>The context.</returns>
        private static StructureContext RepeatContext()
        {
            return new StructureContext(
                new List<InstanceID> { new InstanceID(5) },
                "#5@3",
                "#5@3/iter-1",
                null);
        }

        /// <summary>
        /// A Structure context carrying a Condition-evaluation identity.
        /// </summary>
        /// <returns>The context.</returns>
        private static StructureContext EvaluationContext()
        {
            return new StructureContext(
                new List<InstanceID> { new InstanceID(5) },
                "#5@3",
                null,
                "#5@3/eval-1");
        }
    }
}
