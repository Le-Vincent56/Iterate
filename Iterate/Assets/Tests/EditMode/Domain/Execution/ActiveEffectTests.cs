using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests <see cref="ActiveEffect"/> as a validated tagged union constructed only through its five
    /// per-kind static factories: each factory round-trips its kind with the other operation fields
    /// null, each factory's null guards throw, <see cref="ActiveEffect.IsModification"/> derives from
    /// the kind, and the frequency key composes unchanged.
    /// </summary>
    public sealed class ActiveEffectTests
    {
        [Test]
        public void ForModification_RoundTrips()
        {
            ActiveEffect effect = ActiveEffect.ForModification(
                new InstanceID(1),
                "WB-DEP-001",
                0,
                QuantityTrigger(),
                QuantityChange(),
                Frequency());

            Assert.AreEqual(ActiveEffectKind.Modification, effect.Kind);
            Assert.IsTrue(effect.IsModification);
            Assert.IsNotNull(effect.Operation);
            Assert.IsNull(effect.Rescue);
            Assert.IsNull(effect.Request);
            Assert.IsNull(effect.BoundaryName);
            Assert.AreEqual("WB-DEP-001:0#1", effect.FrequencyKey);
        }

        [Test]
        public void ForReaction_RoundTrips()
        {
            ActiveEffect effect = ActiveEffect.ForReaction(
                new InstanceID(2),
                "WB-DEP-004",
                0,
                QuantityTrigger(),
                QuantityChange(),
                Frequency());

            Assert.AreEqual(ActiveEffectKind.Reaction, effect.Kind);
            Assert.IsFalse(effect.IsModification);
            Assert.IsNotNull(effect.Operation);
            Assert.IsNull(effect.Rescue);
            Assert.IsNull(effect.Request);
            Assert.IsNull(effect.BoundaryName);
        }

        [Test]
        public void ForRescue_RoundTrips()
        {
            ActiveEffect effect = ActiveEffect.ForRescue(
                new InstanceID(3),
                "WB-DEP-007",
                0,
                RescueTrigger(),
                new RescueOperation("RESCUED"),
                Frequency());

            Assert.AreEqual(ActiveEffectKind.Rescue, effect.Kind);
            Assert.IsFalse(effect.IsModification);
            Assert.IsNull(effect.Operation);
            Assert.AreEqual("RESCUED", effect.Rescue.ResultingDisposition);
            Assert.IsNull(effect.Request);
            Assert.IsNull(effect.BoundaryName);
            Assert.AreEqual("WB-DEP-007:0#3", effect.FrequencyKey);
        }

        [Test]
        public void ForAddedExecution_RoundTrips()
        {
            AddedExecutionRequestOperation request = new AddedExecutionRequestOperation(
                new TargetingRule("TRIGGERING_UNIT", string.Empty),
                false);

            ActiveEffect effect = ActiveEffect.ForAddedExecution(
                new InstanceID(4),
                "WB-DIR-001",
                0,
                QuantityTrigger(),
                request,
                Frequency());

            Assert.AreEqual(ActiveEffectKind.AddedExecution, effect.Kind);
            Assert.IsFalse(effect.IsModification);
            Assert.IsNull(effect.Operation);
            Assert.IsNull(effect.Rescue);
            Assert.AreSame(request, effect.Request);
            Assert.IsNull(effect.BoundaryName);
            Assert.AreEqual("WB-DIR-001:0#4", effect.FrequencyKey);
        }

        [Test]
        public void ForBoundary_RoundTrips()
        {
            ActiveEffect effect = ActiveEffect.ForBoundary(
                new InstanceID(5),
                "WB-DIR-002",
                0,
                BoundaryTrigger(),
                QuantityChange(),
                "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL",
                Frequency());

            Assert.AreEqual(ActiveEffectKind.Boundary, effect.Kind);
            Assert.IsFalse(effect.IsModification);
            Assert.IsNotNull(effect.Operation);
            Assert.IsNull(effect.Rescue);
            Assert.IsNull(effect.Request);
            Assert.AreEqual("END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL", effect.BoundaryName);
        }

        [Test]
        public void ForModification_NullOperation_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = ActiveEffect.ForModification(
                new InstanceID(1), "WB-DEP-001", 0, QuantityTrigger(), null, Frequency()));
        }

        [Test]
        public void ForReaction_NullOperation_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = ActiveEffect.ForReaction(
                new InstanceID(2), "WB-DEP-004", 0, QuantityTrigger(), null, Frequency()));
        }

        [Test]
        public void ForRescue_NullRescue_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = ActiveEffect.ForRescue(
                new InstanceID(3), "WB-DEP-007", 0, RescueTrigger(), null, Frequency()));
        }

        [Test]
        public void ForAddedExecution_NullRequest_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = ActiveEffect.ForAddedExecution(
                new InstanceID(4), "WB-DIR-001", 0, QuantityTrigger(), null, Frequency()));
        }

        [Test]
        public void ForBoundary_NullOperation_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = ActiveEffect.ForBoundary(
                new InstanceID(5), "WB-DIR-002", 0, BoundaryTrigger(), null, "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL", Frequency()));
        }

        [Test]
        public void ForBoundary_EmptyBoundaryName_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = ActiveEffect.ForBoundary(
                new InstanceID(5), "WB-DIR-002", 0, BoundaryTrigger(), QuantityChange(), string.Empty, Frequency()));
        }

        [Test]
        public void ForReaction_NullTrigger_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = ActiveEffect.ForReaction(
                new InstanceID(2), "WB-DEP-004", 0, null, QuantityChange(), Frequency()));
        }

        [Test]
        public void ForReaction_NullFrequency_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = ActiveEffect.ForReaction(
                new InstanceID(2), "WB-DEP-004", 0, QuantityTrigger(), QuantityChange(), null));
        }

        /// <summary>
        /// The qualifier-free rescue trigger pair at the pre-operation band.
        /// </summary>
        /// <returns>The trigger descriptor.</returns>
        private static TriggerDescriptor RescueTrigger()
        {
            return new TriggerDescriptor(
                EventFamily.Disposition,
                "SOURCE_EXECUTION_SKIPPED",
                new List<TriggerQualifier>(),
                new EffectTiming(TimingKind.Band, "QUALIFICATION_AND_PRE_OPERATION_INTERVENTION"));
        }

        /// <summary>
        /// The qualifier-free quantity-change trigger pair at the immediate-reaction band.
        /// </summary>
        /// <returns>The trigger descriptor.</returns>
        private static TriggerDescriptor QuantityTrigger()
        {
            return new TriggerDescriptor(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                new List<TriggerQualifier>(),
                new EffectTiming(TimingKind.Band, "IMMEDIATE_RESULT_REACTION"));
        }

        /// <summary>
        /// The boundary trigger pair at the named player-traversal boundary.
        /// </summary>
        /// <returns>The trigger descriptor.</returns>
        private static TriggerDescriptor BoundaryTrigger()
        {
            return new TriggerDescriptor(
                EventFamily.Reaction,
                "BOUNDARY_EFFECT_REQUESTED",
                new List<TriggerQualifier>(),
                new EffectTiming(TimingKind.NamedBoundary, "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL"));
        }

        /// <summary>
        /// The standard constant-operand quantity-change operation.
        /// </summary>
        /// <returns>The operation.</returns>
        private static QuantityChangeOperation QuantityChange()
        {
            return new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1));
        }

        /// <summary>
        /// The standard first-qualifying EXECUTION-scoped frequency.
        /// </summary>
        /// <returns>The frequency.</returns>
        private static EffectFrequency Frequency()
        {
            return new EffectFrequency("FIRST_QUALIFYING_EVENT", "EXECUTION");
        }
    }
}
