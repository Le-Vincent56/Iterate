using System;
using Iterate.Domain.Content;

namespace Iterate.Domain.Content.Tests
{
    /// <summary>
    /// Shared builders for effect-model fixtures used across the definition-record tests.
    /// </summary>
    public static class EffectFixtures
    {
        /// <summary>
        /// Builds a minimal execution-domain quantity-change reaction effect.
        /// </summary>
        /// <returns>The effect definition.</returns>
        public static EffectDefinition QuantityReaction()
        {
            EffectTiming timing = new(TimingKind.Band, "IMMEDIATE_RESULT_REACTION");
            TriggerDescriptor trigger = new(
                EventFamily.Quantity,
                "QUANTITY_CHANGED",
                Array.Empty<TriggerQualifier>(),
                timing
            );
            EffectOperation operation = new QuantityChangeOperation(
                CoreRegister.Value,
                QuantityOperator.Add,
                OperandSpec.FromConstant(1)
            );

            return new EffectDefinition(
                PhaseDomain.Execution,
                trigger,
                operation,
                new TargetingRule("SAME_REGISTER_AS_TRIGGER", string.Empty),
                timing,
                StackingMode.IndependentResolution,
                new EffectFrequency("FIRST_QUALIFYING_EVENT", "EXECUTION")
            );
        }

        /// <summary>
        /// Builds a minimal process-setup-domain configuration-modification effect, as a shop Utility
        /// carries: no runtime trigger, no target.
        /// </summary>
        /// <returns>The effect definition.</returns>
        public static EffectDefinition ConfigurationSetup()
        {
            EffectOperation operation = new ConfigurationModificationOperation("STARTING_BYTES", 1, false);

            return new EffectDefinition(
                PhaseDomain.ProcessSetup,
                null,
                operation,
                null,
                null,
                StackingMode.IndependentResolution,
                null
            );
        }
    }
}
