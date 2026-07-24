using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The closed, fail-fast interpreter turning installed Dependencies' and active Directives'
    /// declarative EXECUTION effects into <see cref="ActiveEffect"/>s. Admits exactly the vocabulary
    /// this engine can honor — the seven trigger pairs keyed to their bands or named boundary, the
    /// closed per-pair qualifier vocabulary, constant-operand quantity changes, the RESCUED-resolving
    /// rescue operation, added-execution request operations with their pair-bound targeting and
    /// cancel-on-invalid false, and the three allowances — and throws on everything else so the
    /// engine never silently under-executes installed content.
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
        /// The post-unit consequence-and-evidence timing-band token.
        /// </summary>
        private const string PostUnitBand = "POST_UNIT_CONSEQUENCE_AND_EVIDENCE";

        /// <summary>
        /// The only named scheduling boundary this engine wires.
        /// </summary>
        private const string PlayerTraversalBoundary = "END_OF_PLAYER_CONTROLLED_SOURCE_TRAVERSAL";

        /// <summary>
        /// The only rescue resulting-disposition token this engine honors.
        /// </summary>
        private const string RescuedDisposition = "RESCUED";

        /// <summary>
        /// The added-execution targeting token selecting the triggering unit.
        /// </summary>
        private const string TriggeringUnitTargeting = "TRIGGERING_UNIT";

        /// <summary>
        /// The added-execution targeting token selecting a Condition's first contained Instruction.
        /// </summary>
        private const string FirstContainedInstructionTargeting = "FIRST_CONTAINED_INSTRUCTION";

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

            return InterpretDeclared(dependency.InstanceID, dependency.Definition.ID.Value, dependency.Definition.Effects);
        }

        /// <summary>
        /// Interprets every EXECUTION-domain effect the active Directive declares, skipping other
        /// phase domains, and returns the interpreted effects in declaration order.
        /// </summary>
        /// <param name="directive">The active Directive instance.</param>
        /// <returns>The interpreted effects; empty when nothing is declared.</returns>
        /// <exception cref="ArgumentException">Thrown when the Directive is null or an EXECUTION effect is uninterpretable.</exception>
        public static IReadOnlyList<ActiveEffect> Interpret(DirectiveInstance directive)
        {
            if (directive == null)
                throw new ArgumentException("Interpretation requires a Directive instance.", nameof(directive));

            return InterpretDeclared(directive.InstanceID, directive.Definition.ID.Value, directive.Definition.Effects);
        }

        /// <summary>
        /// Interprets a declared effect list against the closed vocabulary under one owning origin.
        /// </summary>
        /// <param name="origin">The owning content instance's identity.</param>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="declared">The declared effects.</param>
        /// <returns>The interpreted effects in declaration order.</returns>
        /// <exception cref="ArgumentException">Thrown when an EXECUTION effect is uninterpretable.</exception>
        private static IReadOnlyList<ActiveEffect> InterpretDeclared(
            InstanceID origin,
            string definitionID,
            IReadOnlyList<EffectDefinition> declared
        )
        {
            List<ActiveEffect> effects = new List<ActiveEffect>(declared.Count);
            for (int i = 0; i < declared.Count; i++)
            {
                EffectDefinition effect = declared[i];
                if (effect.PhaseDomain != PhaseDomain.Execution)
                    continue;

                effects.Add(InterpretExecutionEffect(origin, definitionID, i, effect));
            }

            return effects;
        }

        /// <summary>
        /// Interprets one EXECUTION effect against the closed vocabulary.
        /// </summary>
        /// <param name="origin">The owning content instance's identity.</param>
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

            EffectFrequency frequency = RequireKnownFrequency(definitionID, effect.Frequency);
            string subtype = trigger.EventSubtype;

            if (trigger.EventFamily == EventFamily.Operation && subtype == ExecutionEventSubtypes.PrimaryOperationPending)
            {
                RequireBand(definitionID, trigger, ModificationBand);
                RequireQuantityVocabularyQualifiers(definitionID, trigger);
                return ActiveEffect.ForModification(origin, definitionID, effectIndex, trigger, RequireConstantQuantityChange(definitionID, effect.Operation), frequency);
            }

            if (trigger.EventFamily == EventFamily.Operation && subtype == ExecutionEventSubtypes.PrimaryOperationResolved)
            {
                RequireBand(definitionID, trigger, ReactionBand);
                RequireQuantityVocabularyQualifiers(definitionID, trigger);
                return ActiveEffect.ForReaction(origin, definitionID, effectIndex, trigger, RequireConstantQuantityChange(definitionID, effect.Operation), frequency);
            }

            if (trigger.EventFamily == EventFamily.Quantity && subtype == ExecutionEventSubtypes.QuantityChanged)
            {
                RequireBand(definitionID, trigger, ReactionBand);
                RequireQuantityVocabularyQualifiers(definitionID, trigger);
                if (effect.Operation is AddedExecutionRequestOperation)
                {
                    AddedExecutionRequestOperation request = RequireAddedExecutionRequest(definitionID, effect.Operation, TriggeringUnitTargeting);
                    return ActiveEffect.ForAddedExecution(origin, definitionID, effectIndex, trigger, request, frequency);
                }

                return ActiveEffect.ForReaction(origin, definitionID, effectIndex, trigger, RequireConstantQuantityChange(definitionID, effect.Operation), frequency);
            }

            if (trigger.EventFamily == EventFamily.Disposition && subtype == ExecutionEventSubtypes.SourceExecutionSkipped)
            {
                RequireBand(definitionID, trigger, PreOperationBand);
                RequireNoQualifiers(definitionID, trigger, "the rescue trigger pair");
                return ActiveEffect.ForRescue(origin, definitionID, effectIndex, trigger, RequireRescueOperation(definitionID, effect.Operation), frequency);
            }

            if (trigger.EventFamily == EventFamily.Lifecycle && subtype == ExecutionEventSubtypes.RuntimeUnitCompleted)
            {
                RequireBand(definitionID, trigger, PostUnitBand);
                RequirePostUnitQualifiers(definitionID, trigger);
                AddedExecutionRequestOperation request = RequireAddedExecutionRequest(definitionID, effect.Operation, TriggeringUnitTargeting);
                return ActiveEffect.ForAddedExecution(origin, definitionID, effectIndex, trigger, request, frequency);
            }

            if (trigger.EventFamily == EventFamily.Structure && subtype == ExecutionEventSubtypes.ConditionTrue)
            {
                RequireBand(definitionID, trigger, PostUnitBand);
                RequireNoQualifiers(definitionID, trigger, "the CONDITION_TRUE trigger pair");
                AddedExecutionRequestOperation request = RequireAddedExecutionRequest(definitionID, effect.Operation, FirstContainedInstructionTargeting);
                return ActiveEffect.ForAddedExecution(origin, definitionID, effectIndex, trigger, request, frequency);
            }

            if (trigger.EventFamily == EventFamily.Reaction && subtype == ExecutionEventSubtypes.BoundaryEffectRequested)
            {
                string boundaryName = RequireWiredBoundary(definitionID, trigger);
                RequireBoundaryQualifiers(definitionID, trigger);
                return ActiveEffect.ForBoundary(origin, definitionID, effectIndex, trigger, RequireConstantQuantityChange(definitionID, effect.Operation), boundaryName, frequency);
            }

            throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries an unsupported trigger pair '{trigger.EventFamily}/{subtype}'.");
        }

        /// <summary>
        /// Validates that the trigger's timing is the required causal band.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="trigger">The trigger descriptor.</param>
        /// <param name="requiredBand">The band token the pair requires.</param>
        /// <exception cref="ArgumentException">Thrown when the timing is absent, named, or another band.</exception>
        private static void RequireBand(
            string definitionID,
            TriggerDescriptor trigger,
            string requiredBand
        )
        {
            EffectTiming timing = trigger.Timing;
            if (timing is not { Kind: TimingKind.Band })
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries no timing band on its trigger.");

            if (timing.Name != requiredBand)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' pairs subtype '{trigger.EventSubtype}' with an unsupported band '{timing.Name}'.");
        }

        /// <summary>
        /// Validates that the boundary pair's timing names the one wired scheduling boundary and
        /// returns its token.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="trigger">The trigger descriptor.</param>
        /// <returns>The wired boundary token.</returns>
        /// <exception cref="ArgumentException">Thrown when the timing is not a named boundary or names an unwired one.</exception>
        private static string RequireWiredBoundary(string definitionID, TriggerDescriptor trigger)
        {
            EffectTiming timing = trigger.Timing;
            if (timing is not { Kind: TimingKind.NamedBoundary })
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' pairs subtype '{trigger.EventSubtype}' with no named scheduling boundary.");

            if (timing.Name != PlayerTraversalBoundary)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' declares an unwired scheduling boundary '{timing.Name}'.");

            return timing.Name;
        }

        /// <summary>
        /// Validates every trigger qualifier against the closed operation/quantity vocabulary shared
        /// by the pending-operation, resolved-operation, and quantity-change pairs.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="trigger">The trigger descriptor.</param>
        /// <exception cref="ArgumentException">Thrown when any qualifier is outside the closed vocabulary.</exception>
        private static void RequireQuantityVocabularyQualifiers(string definitionID, TriggerDescriptor trigger)
        {
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
        /// Validates the post-unit pair's qualifiers: only the Repeat-context structure qualifier is
        /// evaluable at a unit closure.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="trigger">The trigger descriptor.</param>
        /// <exception cref="ArgumentException">Thrown when any qualifier is outside the pair's vocabulary.</exception>
        private static void RequirePostUnitQualifiers(string definitionID, TriggerDescriptor trigger)
        {
            for (int i = 0; i < trigger.Qualifiers.Count; i++)
            {
                TriggerQualifier qualifier = trigger.Qualifiers[i];
                if (qualifier.Kind != "STRUCTURE_CONTEXT" || qualifier.Value != "INSIDE_REPEAT")
                    throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries an unsupported qualifier '{qualifier.Kind}/{qualifier.Value}' on the post-unit trigger pair.");
            }
        }

        /// <summary>
        /// Validates the boundary pair's qualifiers: register-parity reads only, with the parity
        /// qualifier requiring a register qualifier naming what it reads.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="trigger">The trigger descriptor.</param>
        /// <exception cref="ArgumentException">Thrown when any qualifier is outside the pair's vocabulary or parity lacks a register.</exception>
        private static void RequireBoundaryQualifiers(string definitionID, TriggerDescriptor trigger)
        {
            bool parityDeclared = false;
            bool registerDeclared = false;
            for (int i = 0; i < trigger.Qualifiers.Count; i++)
            {
                TriggerQualifier qualifier = trigger.Qualifiers[i];
                bool known;
                switch (qualifier.Kind)
                {
                    case "PARITY":
                        known = qualifier.Value == "ODD";
                        parityDeclared = true;
                        break;

                    case "REGISTER":
                        known = qualifier.Value is "VALUE" or "SIGNAL" or "SCORE";
                        registerDeclared = true;
                        break;

                    default:
                        known = false;
                        break;
                }

                if (!known)
                    throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries an unsupported qualifier '{qualifier.Kind}/{qualifier.Value}' on the boundary trigger pair.");
            }

            if (parityDeclared && !registerDeclared)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries a PARITY qualifier without a REGISTER qualifier naming the register it reads.");
        }

        /// <summary>
        /// Validates that the trigger carries no qualifiers.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="trigger">The trigger descriptor.</param>
        /// <param name="pairDescription">The pair named in the failure message.</param>
        /// <exception cref="ArgumentException">Thrown when a qualifier is declared.</exception>
        private static void RequireNoQualifiers(
            string definitionID,
            TriggerDescriptor trigger,
            string pairDescription
        )
        {
            if (trigger.Qualifiers.Count > 0)
            {
                TriggerQualifier declared = trigger.Qualifiers[0];
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries a qualifier '{declared.Kind}/{declared.Value}' on {pairDescription}; the pair admits none.");
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
        /// Validates that the operation is an added-execution request with the pair's required
        /// targeting and cancel-on-invalid false.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="operation">The declared operation.</param>
        /// <param name="requiredTargeting">The targeting token the trigger pair requires.</param>
        /// <returns>The operation as an added-execution request.</returns>
        /// <exception cref="ArgumentException">Thrown when the operation kind, targeting, or cancel flag is unsupported.</exception>
        private static AddedExecutionRequestOperation RequireAddedExecutionRequest(
            string definitionID,
            EffectOperation operation,
            string requiredTargeting
        )
        {
            if (operation is not AddedExecutionRequestOperation request)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' pairs its trigger with an unsupported operation kind '{operation.Kind}'.");

            if (request.Target.Kind != requiredTargeting)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' declares added-execution targeting '{request.Target.Kind}'; only '{requiredTargeting}' is interpretable on this trigger pair.");

            if (request.CancelOnInvalid)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' declares CancelOnInvalid = true; only false is interpretable.");

            return request;
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

            if (frequency.Allowance != "FIRST_QUALIFYING_EVENT" && frequency.Allowance != "EVERY_QUALIFYING_EVENT" && frequency.Allowance != "ONCE")
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries an unsupported allowance '{frequency.Allowance}'.");

            return frequency;
        }
    }
}