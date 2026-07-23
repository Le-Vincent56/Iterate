using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests <see cref="ActiveEffect"/> as a validated tagged union: a rescue-kind effect carries a
    /// <see cref="RescueOperation"/> and no quantity change, the other kinds the reverse,
    /// <see cref="ActiveEffect.IsModification"/> derives from the kind, and the frequency key
    /// composes unchanged.
    /// </summary>
    public sealed class ActiveEffectTests
    {
        [Test]
        public void Constructor_RescueKind_RoundTrips()
        {
            ActiveEffect effect = new ActiveEffect(
                new InstanceID(3),
                "WB-DEP-007",
                0,
                RescueTrigger(),
                ActiveEffectKind.Rescue,
                null,
                new RescueOperation("RESCUED"),
                Frequency());

            Assert.AreEqual(ActiveEffectKind.Rescue, effect.Kind);
            Assert.IsNull(effect.Operation);
            Assert.AreEqual("RESCUED", effect.Rescue.ResultingDisposition);
            Assert.IsFalse(effect.IsModification);
            Assert.AreEqual("WB-DEP-007:0#3", effect.FrequencyKey);
        }

        [Test]
        public void Constructor_ModificationKind_DerivesIsModification()
        {
            ActiveEffect effect = new ActiveEffect(
                new InstanceID(1),
                "WB-DEP-001",
                0,
                RescueTrigger(),
                ActiveEffectKind.Modification,
                QuantityChange(),
                null,
                Frequency());

            Assert.AreEqual(ActiveEffectKind.Modification, effect.Kind);
            Assert.IsTrue(effect.IsModification);
            Assert.IsNull(effect.Rescue);
        }

        [Test]
        public void Constructor_ReactionKind_IsNotModification()
        {
            ActiveEffect effect = new ActiveEffect(
                new InstanceID(2),
                "WB-DEP-004",
                0,
                RescueTrigger(),
                ActiveEffectKind.Reaction,
                QuantityChange(),
                null,
                Frequency());

            Assert.AreEqual(ActiveEffectKind.Reaction, effect.Kind);
            Assert.IsFalse(effect.IsModification);
        }

        [Test]
        public void Constructor_RescueKindWithQuantityChange_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ActiveEffect(
                new InstanceID(3),
                "WB-DEP-007",
                0,
                RescueTrigger(),
                ActiveEffectKind.Rescue,
                QuantityChange(),
                new RescueOperation("RESCUED"),
                Frequency()));
        }

        [Test]
        public void Constructor_RescueKindWithoutRescue_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ActiveEffect(
                new InstanceID(3),
                "WB-DEP-007",
                0,
                RescueTrigger(),
                ActiveEffectKind.Rescue,
                null,
                null,
                Frequency()));
        }

        [Test]
        public void Constructor_ModificationKindWithoutOperation_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ActiveEffect(
                new InstanceID(1),
                "WB-DEP-001",
                0,
                RescueTrigger(),
                ActiveEffectKind.Modification,
                null,
                null,
                Frequency()));
        }

        [Test]
        public void Constructor_ReactionKindWithRescue_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ActiveEffect(
                new InstanceID(2),
                "WB-DEP-004",
                0,
                RescueTrigger(),
                ActiveEffectKind.Reaction,
                QuantityChange(),
                new RescueOperation("RESCUED"),
                Frequency()));
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
