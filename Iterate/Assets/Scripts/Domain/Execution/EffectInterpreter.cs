using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The closed, fail-fast interpreter turning an installed Dependency's declarative EXECUTION
    /// effects into <see cref="ActiveEffect"/>s. Admits exactly the vocabulary this engine can honor —
    /// the four trigger pairs keyed to their bands, the closed qualifier vocabulary, constant-operand
    /// quantity changes, the RESCUED-resolving rescue operation on the qualifier-free rescue pair, and
    /// the two allowances — and throws on everything else so the engine never silently under-executes
    /// installed content.
    /// </summary>
    public static class EffectInterpreter
    {
        /// <summary>
        /// The pre-operation intervention timing-band token.
        /// </summary>
        private const string PreOperationBand = "QUALIFICATION_AND_PRE_OPERATION_INTERVENTION";

        /// <summary>
        /// The modification timing-band token.
        /// </summary>
        private const string ModificationBand = "OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION";

        /// <summary>
        /// The immediate-reaction timing-band token.
        /// </summary>
        private const string ReactionBand = "IMMEDIATE_RESULT_REACTION";

        /// <summary>
        /// The only rescue resulting-disposition token this engine honors.
        /// </summary>
        private const string RescuedDisposition = "RESCUED";

        /// <summary>
        /// Interprets every EXECUTION-domain effect the Dependency declares, skipping other phase
        /// domains, and returns the interpreted effects in declaration order.
        /// </summary>
        /// <param name="dependency">The installed Dependency instance.</param>
        /// <returns>The interpreted effects; empty when nothing is declared.</returns>
        /// <exception cref="ArgumentException">Thrown when the Dependency is null or an EXECUTION effect is uninterpretable.</exception>
        public static IReadOnlyList<ActiveEffect> Interpret(DependencyInstance dependency)
        {
            if (dependency == null)
                throw new ArgumentException("Interpretation requires a Dependency instance.", nameof(dependency));

            IReadOnlyList<EffectDefinition> declared = dependency.Definition.Effects;
            List<ActiveEffect> effects = new List<ActiveEffect>(declared.Count);
            string definitionID = dependency.Definition.ID.Value;

            for (int i = 0; i < declared.Count; i++)
            {
                EffectDefinition effect = declared[i];
                if (effect.PhaseDomain != PhaseDomain.Execution)
                    continue;

                effects.Add(InterpretExecutionEffect(dependency.InstanceID, definitionID, i, effect));
            }

            return effects;
        }

        /// <summary>
        /// Interprets one EXECUTION effect against the closed vocabulary.
        /// </summary>
        /// <param name="origin">The owning Dependency instance's identity.</param>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="effectIndex">The effect's index within the definition.</param>
        /// <param name="effect">The declared effect.</param>
        /// <returns>The interpreted effect.</returns>
        /// <exception cref="ArgumentException">Thrown when any token falls outside the closed vocabulary.</exception>
        private static ActiveEffect InterpretExecutionEffect(
            InstanceID origin,
            string definitionID,
            int effectIndex,
            EffectDefinition effect
        )
        {
            TriggerDescriptor trigger = effect.Trigger;
            if (trigger == null)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries no trigger.");

            ActiveEffectKind kind = RequireKnownBoundary(definitionID, trigger);
            RequireKnownQualifiers(definitionID, trigger, kind);

            QuantityChangeOperation operation = null;
            RescueOperation rescue = null;
            if (kind == ActiveEffectKind.Rescue)
                rescue = RequireRescueOperation(definitionID, effect.Operation);
            else
                operation = RequireConstantQuantityChange(definitionID, effect.Operation);

            EffectFrequency frequency = RequireKnownFrequency(definitionID, effect.Frequency);

            return new ActiveEffect(origin, definitionID, effectIndex, trigger, kind, operation, rescue, frequency);
        }

        /// <summary>
        /// Validates the trigger's family/subtype pair and timing band against the closed set and
        /// returns the participation kind the pair selects.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="trigger">The trigger descriptor.</param>
        /// <returns>The participation kind the trigger pair selects.</returns>
        /// <exception cref="ArgumentException">Thrown when the pair or band is outside the closed set.</exception>
        private static ActiveEffectKind RequireKnownBoundary(string definitionID, TriggerDescriptor trigger)
        {
            string subtype = trigger.EventSubtype;
            bool pendingOperation = trigger.EventFamily == EventFamily.Operation && subtype == ExecutionEventSubtypes.PrimaryOperationPending;
            bool resolvedOperation = trigger.EventFamily == EventFamily.Operation && subtype == ExecutionEventSubtypes.PrimaryOperationResolved;
            bool quantityChanged = trigger.EventFamily == EventFamily.Quantity && subtype == ExecutionEventSubtypes.QuantityChanged;
            bool executionSkipped = trigger.EventFamily == EventFamily.Disposition && subtype == ExecutionEventSubtypes.SourceExecutionSkipped;

            if (!pendingOperation && !resolvedOperation && !quantityChanged && !executionSkipped)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries an unsupported trigger pair '{trigger.EventFamily}/{subtype}'.");

            EffectTiming timing = trigger.Timing;
            if (timing is not { Kind: TimingKind.Band })
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries no timing band on its trigger.");

            ActiveEffectKind kind;
            string requiredBand;
            if (pendingOperation)
            {
                kind = ActiveEffectKind.Modification;
                requiredBand = ModificationBand;
            }
            else if (executionSkipped)
            {
                kind = ActiveEffectKind.Rescue;
                requiredBand = PreOperationBand;
            }
            else
            {
                kind = ActiveEffectKind.Reaction;
                requiredBand = ReactionBand;
            }

            if (timing.Name != requiredBand)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' pairs subtype '{subtype}' with an unsupported band '{timing.Name}'.");

            return kind;
        }

        /// <summary>
        /// Validates every trigger qualifier against the closed kind/value vocabulary. The rescue
        /// pair admits no qualifiers at all — nothing in the closed vocabulary is evaluable against
        /// a skip.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="trigger">The trigger descriptor.</param>
        /// <param name="kind">The participation kind the trigger pair selected.</param>
        /// <exception cref="ArgumentException">Thrown when any qualifier is outside the closed vocabulary.</exception>
        private static void RequireKnownQualifiers(
            string definitionID,
            TriggerDescriptor trigger,
            ActiveEffectKind kind
        )
        {
            if (kind == ActiveEffectKind.Rescue)
            {
                if (trigger.Qualifiers.Count > 0)
                {
                    TriggerQualifier declared = trigger.Qualifiers[0];
                    throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries a qualifier '{declared.Kind}/{declared.Value}' on the rescue trigger pair; the pair admits none.");
                }

                return;
            }

            for (int i = 0; i < trigger.Qualifiers.Count; i++)
            {
                TriggerQualifier qualifier = trigger.Qualifiers[i];
                bool known;
                switch (qualifier.Kind)
                {
                    case "OPERATION_CLASS":
                        known = qualifier.Value is "FIXED_ADDITION" or "PLAYER_INSTRUCTION" or "VALUE_ADD_SIGNAL";
                        break;

                    case "REGISTER":
                        known = qualifier.Value is "VALUE" or "SIGNAL" or "SCORE";
                        break;

                    case "ACTUAL_DELTA_SIGN":
                        known = qualifier.Value == "POSITIVE";
                        break;

                    default:
                        known = false;
                        break;
                }

                if (!known)
                    throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries an unsupported qualifier '{qualifier.Kind}/{qualifier.Value}'.");
            }
        }

        /// <summary>
        /// Validates that the rescue pair's operation is a rescue resolving to RESCUED.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="operation">The declared operation.</param>
        /// <returns>The operation as a rescue.</returns>
        /// <exception cref="ArgumentException">Thrown when the operation kind or resulting disposition is unsupported.</exception>
        private static RescueOperation RequireRescueOperation(string definitionID, EffectOperation operation)
        {
            if (operation is not RescueOperation rescue)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' pairs the rescue trigger with an unsupported operation kind '{operation.Kind}'.");

            if (rescue.ResultingDisposition != RescuedDisposition)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries a rescue resulting disposition '{rescue.ResultingDisposition}'; only '{RescuedDisposition}' is interpretable.");

            return rescue;
        }

        /// <summary>
        /// Validates that the operation is a quantity change with a constant operand.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="operation">The declared operation.</param>
        /// <returns>The operation as a quantity change.</returns>
        /// <exception cref="ArgumentException">Thrown when the operation kind or operand source is unsupported.</exception>
        private static QuantityChangeOperation RequireConstantQuantityChange(string definitionID, EffectOperation operation)
        {
            if (operation is not QuantityChangeOperation quantityChange)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries an unsupported operation kind '{operation.Kind}'.");

            if (quantityChange.Operand.Source != OperandSource.Constant)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries an unsupported operand source '{quantityChange.Operand.Source}'.");

            return quantityChange;
        }

        /// <summary>
        /// Validates that the frequency is present with an allowance from the closed set.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="frequency">The declared frequency.</param>
        /// <returns>The frequency unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the frequency is absent or its allowance unsupported.</exception>
        private static EffectFrequency RequireKnownFrequency(string definitionID, EffectFrequency frequency)
        {
            if (frequency == null)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries no frequency.");

            if (frequency.Allowance != "FIRST_QUALIFYING_EVENT" && frequency.Allowance != "EVERY_QUALIFYING_EVENT")
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries an unsupported allowance '{frequency.Allowance}'.");

            return frequency;
        }
    }
}