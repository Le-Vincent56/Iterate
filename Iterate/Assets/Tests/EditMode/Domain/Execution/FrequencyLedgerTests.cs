using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the per-execution <see cref="FrequencyLedger"/>: first-qualifying keys consume exactly
    /// once per execution and reset on clear, every-qualifying effects never consume, and distinct
    /// frequency keys consume independently.
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

            return new ActiveEffect(
                new InstanceID(instance),
                definitionID,
                effectIndex,
                trigger,
                ActiveEffectKind.Reaction,
                new QuantityChangeOperation(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(1)),
                null,
                new EffectFrequency(allowance, "EXECUTION"));
        }
    }
}
