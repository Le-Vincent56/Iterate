using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the per-execution <see cref="FrequencyLedger"/>: first-qualifying and once-per-execution
    /// keys consume exactly once per execution and reset on clear, every-qualifying effects never
    /// consume, and distinct frequency keys consume independently. A source-execution-scoped
    /// allowance keys additionally on the triggering unit, so one attachment may commit once under
    /// each unit it observes.
    /// </summary>
    public sealed class FrequencyLedgerTests
    {
        [Test]
        public void FirstQualifying_EligibleFresh_ConsumedThenClearedRestores()
        {
            FrequencyLedger ledger = new FrequencyLedger();
            ActiveEffect effect = Effect("WB-DEP-901", 0, 1, "FIRST_QUALIFYING_EVENT");

            Assert.IsTrue(ledger.IsEligible(effect));

            ledger.Consume(effect);
            Assert.IsFalse(ledger.IsEligible(effect));

            ledger.Clear();
            Assert.IsTrue(ledger.IsEligible(effect));
        }

        [Test]
        public void EveryQualifying_StaysEligibleThroughRepeatedConsume()
        {
            FrequencyLedger ledger = new FrequencyLedger();
            ActiveEffect effect = Effect("WB-DEP-904", 0, 2, "EVERY_QUALIFYING_EVENT");

            ledger.Consume(effect);
            ledger.Consume(effect);

            Assert.IsTrue(ledger.IsEligible(effect));
        }

        [Test]
        public void Once_EligibleFresh_ConsumedThenClearedRestores()
        {
            FrequencyLedger ledger = new FrequencyLedger();
            ActiveEffect effect = Effect("WB-DIR-001", 0, 7, "ONCE");

            Assert.IsTrue(ledger.IsEligible(effect));

            ledger.Consume(effect);
            Assert.IsFalse(ledger.IsEligible(effect));

            ledger.Clear();
            Assert.IsTrue(ledger.IsEligible(effect));
        }

        [Test]
        public void Once_DistinctInstances_ConsumeIndependently()
        {
            FrequencyLedger ledger = new FrequencyLedger();
            ActiveEffect first = Effect("WB-DIR-001", 0, 7, "ONCE");
            ActiveEffect second = Effect("WB-DIR-001", 0, 8, "ONCE");

            ledger.Consume(first);

            Assert.IsFalse(ledger.IsEligible(first));
            Assert.IsTrue(ledger.IsEligible(second));
        }

        [Test]
        public void DistinctInstances_SameDefinition_ConsumeIndependently()
        {
            FrequencyLedger ledger = new FrequencyLedger();
            ActiveEffect first = Effect("WB-DEP-901", 0, 1, "FIRST_QUALIFYING_EVENT");
            ActiveEffect second = Effect("WB-DEP-901", 0, 2, "FIRST_QUALIFYING_EVENT");

            ledger.Consume(first);

            Assert.IsFalse(ledger.IsEligible(first));
            Assert.IsTrue(ledger.IsEligible(second));
        }

        [Test]
        public void DistinctEffectIndices_SameInstance_ConsumeIndependently()
        {
            FrequencyLedger ledger = new FrequencyLedger();
            ActiveEffect first = Effect("WB-DEP-905", 0, 3, "FIRST_QUALIFYING_EVENT");
            ActiveEffect second = Effect("WB-DEP-905", 1, 3, "FIRST_QUALIFYING_EVENT");

            ledger.Consume(second);

            Assert.IsTrue(ledger.IsEligible(first));
            Assert.IsFalse(ledger.IsEligible(second));
        }

        [Test]
        public void SourceExecutionScope_ConsumedUnderOneUnit_StaysEligibleUnderAnother()
        {
            FrequencyLedger ledger = new FrequencyLedger();
            ActiveEffect effect = ScopedEffect("WB-PAT-002", 0, 60, "ONCE", "SOURCE_EXECUTION");
            RuntimeUnitID first = new RuntimeUnitID(1);
            RuntimeUnitID second = new RuntimeUnitID(2);

            ledger.Consume(effect, first);

            Assert.IsFalse(ledger.IsEligible(effect, first));
            Assert.IsTrue(ledger.IsEligible(effect, second));
        }

        [Test]
        public void SourceExecutionScope_ConsumedUnderBothUnits_IneligibleUnderBoth()
        {
            FrequencyLedger ledger = new FrequencyLedger();
            ActiveEffect effect = ScopedEffect("WB-PAT-002", 0, 60, "ONCE", "SOURCE_EXECUTION");
            RuntimeUnitID first = new RuntimeUnitID(1);
            RuntimeUnitID second = new RuntimeUnitID(2);

            ledger.Consume(effect, first);
            ledger.Consume(effect, second);

            Assert.IsFalse(ledger.IsEligible(effect, first));
            Assert.IsFalse(ledger.IsEligible(effect, second));
        }

        [Test]
        public void SourceExecutionScope_ClearRestoresEveryUnitKey()
        {
            FrequencyLedger ledger = new FrequencyLedger();
            ActiveEffect effect = ScopedEffect("WB-PAT-002", 0, 60, "ONCE", "SOURCE_EXECUTION");
            RuntimeUnitID unit = new RuntimeUnitID(1);

            ledger.Consume(effect, unit);
            ledger.Clear();

            Assert.IsTrue(ledger.IsEligible(effect, unit));
        }

        [Test]
        public void SourceExecutionScope_DistinctAttachments_ConsumeIndependentlyUnderOneUnit()
        {
            FrequencyLedger ledger = new FrequencyLedger();
            ActiveEffect first = ScopedEffect("WB-PAT-002", 0, 60, "ONCE", "SOURCE_EXECUTION");
            ActiveEffect second = ScopedEffect("WB-PAT-002", 0, 61, "ONCE", "SOURCE_EXECUTION");
            RuntimeUnitID unit = new RuntimeUnitID(1);

            ledger.Consume(first, unit);

            Assert.IsFalse(ledger.IsEligible(first, unit));
            Assert.IsTrue(ledger.IsEligible(second, unit));
        }

        [Test]
        public void SourceExecutionScope_EveryQualifying_NeverConsumes()
        {
            FrequencyLedger ledger = new FrequencyLedger();
            ActiveEffect effect = ScopedEffect("WB-PAT-005", 0, 62, "EVERY_QUALIFYING_EVENT", "SOURCE_EXECUTION");
            RuntimeUnitID unit = new RuntimeUnitID(1);

            ledger.Consume(effect, unit);

            Assert.IsTrue(ledger.IsEligible(effect, unit));
        }

        [Test]
        public void SourceExecutionScope_LimitedWithNoUnit_ThrowsAsDefect()
        {
            FrequencyLedger ledger = new FrequencyLedger();
            ActiveEffect effect = ScopedEffect("WB-PAT-002", 0, 60, "ONCE", "SOURCE_EXECUTION");

            Assert.Throws<InvalidOperationException>(() => _ = ledger.IsEligible(effect, null));
            Assert.Throws<InvalidOperationException>(() => ledger.Consume(effect, null));
        }

        [Test]
        public void ExecutionScope_IgnoresTriggeringUnit()
        {
            FrequencyLedger ledger = new FrequencyLedger();
            ActiveEffect effect = ScopedEffect("WB-DIR-001", 0, 7, "ONCE", "EXECUTION");
            RuntimeUnitID first = new RuntimeUnitID(1);
            RuntimeUnitID second = new RuntimeUnitID(2);

            ledger.Consume(effect, first);

            Assert.IsFalse(ledger.IsEligible(effect, second));
        }

        [Test]
        public void DeclaredScope_IgnoresTriggeringUnit()
        {
            FrequencyLedger ledger = new FrequencyLedger();
            ActiveEffect effect = ScopedEffect("WB-DEP-901", 0, 1, "FIRST_QUALIFYING_EVENT", "DECLARED_SCOPE");
            RuntimeUnitID first = new RuntimeUnitID(1);
            RuntimeUnitID second = new RuntimeUnitID(2);

            ledger.Consume(effect, first);

            Assert.IsFalse(ledger.IsEligible(effect, second));
        }

        [Test]
        public void SingleArgumentOverloads_BehaveAsNullUnit()
        {
            FrequencyLedger ledger = new FrequencyLedger();
            ActiveEffect effect = ScopedEffect("WB-DIR-001", 0, 7, "ONCE", "EXECUTION");

            ledger.Consume(effect);

            Assert.IsFalse(ledger.IsEligible(effect));
            Assert.IsFalse(ledger.IsEligible(effect, new RuntimeUnitID(1)));
        }

        /// <summary>
        /// Builds a reaction-shaped active effect with the given identity, allowance, and reset scope.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="effectIndex">The effect's index within the definition.</param>
        /// <param name="instance">The owning instance's identity value.</param>
        /// <param name="allowance">The frequency-allowance token.</param>
        /// <param name="scope">The frequency reset-scope token.</param>
        /// <returns>The active effect.</returns>
        private static ActiveEffect ScopedEffect(
            string definitionID,
            int effectIndex,
            int instance,
            string allowance,
            string scope)
        {
            EffectTiming timing = new EffectTiming(TimingKind.Band, "IMMEDIATE_RESULT_REACTION");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                new List<TriggerQualifier> { new TriggerQualifier("REGISTER", "SIGNAL") },
                timing);

            return ActiveEffect.ForReaction(
                new InstanceID(instance),
                definitionID,
                effectIndex,
                trigger,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                new EffectFrequency(allowance, scope));
        }

        /// <summary>
        /// Builds a reaction-shaped active effect with the given identity and allowance.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="effectIndex">The effect's index within the definition.</param>
        /// <param name="instance">The owning instance's identity value.</param>
        /// <param name="allowance">The frequency-allowance token.</param>
        /// <returns>The active effect.</returns>
        private static ActiveEffect Effect(
            string definitionID,
            int effectIndex,
            int instance,
            string allowance)
        {
            EffectTiming timing = new EffectTiming(TimingKind.Band, "IMMEDIATE_RESULT_REACTION");
            TriggerDescriptor trigger = new TriggerDescriptor(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                new List<TriggerQualifier> { new TriggerQualifier("REGISTER", "SIGNAL") },
                timing);

            return ActiveEffect.ForReaction(
                new InstanceID(instance),
                definitionID,
                effectIndex,
                trigger,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                new EffectFrequency(allowance, "EXECUTION"));
        }
    }
}
