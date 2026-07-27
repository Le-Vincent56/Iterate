using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests <see cref="ActiveEffect"/> as a validated tagged union constructed only through its
    /// per-kind static factories: each factory round-trips its kind with the other operation fields
    /// null, each factory's null guards throw, <see cref="ActiveEffect.IsModification"/> derives from
    /// the kind, and the frequency key composes unchanged. The host-socketed derivation is legal
    /// exactly on the kinds a Patch can declare and refuses the rest.
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
        public void ForOperationModification_RoundTrips()
        {
            OperationModificationOperation operandChange = new OperationModificationOperation(1);

            ActiveEffect effect = ActiveEffect.ForOperationModification(
                new InstanceID(6),
                "WB-PAT-001",
                0,
                PendingOperationTrigger(),
                operandChange,
                Frequency());

            Assert.AreEqual(ActiveEffectKind.Modification, effect.Kind);
            Assert.IsTrue(effect.IsModification);
            Assert.AreSame(operandChange, effect.OperationModification);
            Assert.IsNull(effect.Operation);
            Assert.IsNull(effect.Rescue);
            Assert.IsNull(effect.Request);
            Assert.IsNull(effect.TargetLockUpdate);
            Assert.IsNull(effect.BoundaryName);
            Assert.IsNull(effect.HostInstance);
            Assert.AreEqual("WB-PAT-001:0#6", effect.FrequencyKey);
        }

        [Test]
        public void ForTargetLock_RoundTrips()
        {
            TargetLockUpdateOperation update = new TargetLockUpdateOperation(
                new TargetingRule("MOST_RECENT_QUALIFYING_UNIT", string.Empty));

            ActiveEffect effect = ActiveEffect.ForTargetLock(
                new InstanceID(7),
                "WB-DIR-003",
                0,
                QuantityTrigger(),
                update,
                Frequency());

            Assert.AreEqual(ActiveEffectKind.TargetLock, effect.Kind);
            Assert.IsFalse(effect.IsModification);
            Assert.AreSame(update, effect.TargetLockUpdate);
            Assert.IsNull(effect.Operation);
            Assert.IsNull(effect.Rescue);
            Assert.IsNull(effect.Request);
            Assert.IsNull(effect.OperationModification);
            Assert.IsNull(effect.BoundaryName);
            Assert.IsNull(effect.HostInstance);
            Assert.AreEqual("WB-DIR-003:0#7", effect.FrequencyKey);
        }

        [Test]
        public void ForBoundaryCreator_RoundTrips()
        {
            AddedExecutionRequestOperation request = new AddedExecutionRequestOperation(
                new TargetingRule("LOCKED_TARGET", string.Empty),
                true);

            ActiveEffect effect = ActiveEffect.ForBoundaryCreator(
                new InstanceID(8),
                "WB-DIR-003",
                1,
                BoundaryTrigger(),
                request,
                "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL",
                Frequency());

            Assert.AreEqual(ActiveEffectKind.AddedExecution, effect.Kind);
            Assert.AreSame(request, effect.Request);
            Assert.AreEqual("END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL", effect.BoundaryName);
            Assert.IsNull(effect.Operation);
            Assert.IsNull(effect.Rescue);
            Assert.IsNull(effect.TargetLockUpdate);
            Assert.IsNull(effect.OperationModification);
            Assert.AreEqual("WB-DIR-003:1#8", effect.FrequencyKey);
        }

        [Test]
        public void ForAddedExecution_LeavesBoundaryNameNull()
        {
            ActiveEffect effect = ActiveEffect.ForAddedExecution(
                new InstanceID(9),
                "WB-PAT-002",
                0,
                QuantityTrigger(),
                new AddedExecutionRequestOperation(new TargetingRule("OWN_HOST", string.Empty), false),
                Frequency());

            Assert.IsNull(effect.BoundaryName);
        }

        [Test]
        public void WithHostInstance_OnModification_CarriesHost()
        {
            ActiveEffect socketed = ActiveEffect.ForOperationModification(
                new InstanceID(10),
                "WB-PAT-001",
                0,
                PendingOperationTrigger(),
                new OperationModificationOperation(1),
                Frequency()).WithHostInstance(new InstanceID(99));

            Assert.AreEqual(new InstanceID(99), socketed.HostInstance);
            Assert.AreEqual(ActiveEffectKind.Modification, socketed.Kind);
            Assert.AreEqual(1, socketed.OperationModification.OperandDelta);
        }

        [Test]
        public void WithHostInstance_OnReaction_CarriesHost()
        {
            ActiveEffect socketed = ActiveEffect.ForReaction(
                new InstanceID(11),
                "WB-PAT-005",
                0,
                QuantityTrigger(),
                QuantityChange(),
                Frequency()).WithHostInstance(new InstanceID(98));

            Assert.AreEqual(new InstanceID(98), socketed.HostInstance);
            Assert.AreEqual(ActiveEffectKind.Reaction, socketed.Kind);
        }

        [Test]
        public void WithHostInstance_OnAddedExecution_CarriesHost()
        {
            ActiveEffect socketed = ActiveEffect.ForAddedExecution(
                new InstanceID(12),
                "WB-PAT-002",
                0,
                QuantityTrigger(),
                new AddedExecutionRequestOperation(new TargetingRule("OWN_HOST", string.Empty), false),
                Frequency()).WithHostInstance(new InstanceID(97));

            Assert.AreEqual(new InstanceID(97), socketed.HostInstance);
            Assert.AreEqual(ActiveEffectKind.AddedExecution, socketed.Kind);
        }

        [Test]
        public void WithHostInstance_PreservesFrequencyKey()
        {
            ActiveEffect bare = ActiveEffect.ForReaction(
                new InstanceID(13),
                "WB-PAT-005",
                0,
                QuantityTrigger(),
                QuantityChange(),
                Frequency());

            ActiveEffect socketed = bare.WithHostInstance(new InstanceID(96));

            Assert.AreEqual(bare.FrequencyKey, socketed.FrequencyKey);
            Assert.AreEqual("WB-PAT-005:0#13", socketed.FrequencyKey);
        }

        [Test]
        public void WithHostInstance_OnRescue_Throws()
        {
            ActiveEffect rescue = ActiveEffect.ForRescue(
                new InstanceID(14),
                "WB-DEP-007",
                0,
                RescueTrigger(),
                new RescueOperation("RESCUED"),
                Frequency());

            Assert.Throws<ArgumentException>(() => _ = rescue.WithHostInstance(new InstanceID(95)));
        }

        [Test]
        public void WithHostInstance_OnBoundary_Throws()
        {
            ActiveEffect boundary = ActiveEffect.ForBoundary(
                new InstanceID(15),
                "WB-DIR-002",
                0,
                BoundaryTrigger(),
                QuantityChange(),
                "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL",
                Frequency());

            Assert.Throws<ArgumentException>(() => _ = boundary.WithHostInstance(new InstanceID(94)));
        }

        [Test]
        public void WithHostInstance_OnTargetLock_Throws()
        {
            ActiveEffect lockUpdate = ActiveEffect.ForTargetLock(
                new InstanceID(16),
                "WB-DIR-003",
                0,
                QuantityTrigger(),
                new TargetLockUpdateOperation(new TargetingRule("MOST_RECENT_QUALIFYING_UNIT", string.Empty)),
                Frequency());

            Assert.Throws<ArgumentException>(() => _ = lockUpdate.WithHostInstance(new InstanceID(93)));
        }

        [Test]
        public void WithHostInstance_OnBoundaryCreator_Throws()
        {
            ActiveEffect creator = ActiveEffect.ForBoundaryCreator(
                new InstanceID(17),
                "WB-DIR-003",
                1,
                BoundaryTrigger(),
                new AddedExecutionRequestOperation(new TargetingRule("LOCKED_TARGET", string.Empty), true),
                "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL",
                Frequency());

            Assert.Throws<ArgumentException>(() => _ = creator.WithHostInstance(new InstanceID(92)));
        }

        [Test]
        public void ForTargetLock_NullUpdate_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = ActiveEffect.ForTargetLock(
                new InstanceID(7), "WB-DIR-003", 0, QuantityTrigger(), null, Frequency()));
        }

        [Test]
        public void ForOperationModification_NullOperation_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = ActiveEffect.ForOperationModification(
                new InstanceID(6), "WB-PAT-001", 0, PendingOperationTrigger(), null, Frequency()));
        }

        [Test]
        public void ForBoundaryCreator_EmptyBoundaryName_Throws()
        {
            AddedExecutionRequestOperation request = new AddedExecutionRequestOperation(
                new TargetingRule("LOCKED_TARGET", string.Empty),
                true);

            Assert.Throws<ArgumentException>(() => _ = ActiveEffect.ForBoundaryCreator(
                new InstanceID(8), "WB-DIR-003", 1, BoundaryTrigger(), request, string.Empty, Frequency()));
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
        /// The qualifier-free pending-operation trigger pair at the modification band.
        /// </summary>
        /// <returns>The trigger descriptor.</returns>
        private static TriggerDescriptor PendingOperationTrigger()
        {
            return new TriggerDescriptor(
                EventFamily.Operation,
                "PRIMARY_OPERATION_PENDING",
                new List<TriggerQualifier>(),
                new EffectTiming(TimingKind.Band, "OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION"));
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
