using System;
using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The closed, fail-fast interpreter turning installed Dependencies', active Directives', and
    /// attached Patches' declarative EXECUTION effects into <see cref="ActiveEffect"/>s. Admits exactly
    /// the vocabulary this engine can honor — the seven trigger pairs keyed to their bands or named
    /// boundary, the closed per-pair qualifier vocabulary split by whether the effect is host-socketed,
    /// constant-operand quantity changes, host operand adjustments, the RESCUED-resolving rescue
    /// operation, added-execution request operations with their pair-bound targeting and cancel-on-
    /// invalid flag, target-lock updates selecting the most recent qualifying unit, the three
    /// allowances, and the three reset scopes — and throws on everything else so the engine never
    /// silently under-executes installed content.
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
        /// The targeting token a host-socketed effect declares to name its own host.
        /// </summary>
        private const string OwnHostTargeting = "OWN_HOST";

        /// <summary>
        /// The added-execution targeting token selecting whatever the creator's target lock holds.
        /// </summary>
        private const string LockedTargetTargeting = "LOCKED_TARGET";

        /// <summary>
        /// The only target-lock selection token this engine resolves.
        /// </summary>
        private const string MostRecentQualifyingUnitSelection = "MOST_RECENT_QUALIFYING_UNIT";

        /// <summary>
        /// The operation-class qualifier value restricting a socketed reaction to its own host.
        /// </summary>
        private const string HostInstructionQualifier = "HOST_INSTRUCTION";

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

            return InterpretDeclared(dependency.InstanceID, dependency.Definition.ID.Value, dependency.Definition.Effects, null);
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

            return InterpretDeclared(directive.InstanceID, directive.Definition.ID.Value, directive.Definition.Effects, null);
        }
        
        /// <summary>
        /// Interprets every EXECUTION-domain effect a Process rule declares. The effect origin is the
        /// rule instance, so a rule's effects are attributable exactly like any other content's.
        /// </summary>
        /// <param name="rule">The Process-rule instance.</param>
        /// <returns>The interpreted effects.</returns>
        /// <exception cref="ArgumentException">Thrown when the rule is null or an effect is uninterpretable.</exception>
        public static IReadOnlyList<ActiveEffect> Interpret(ProcessRuleInstance rule)
        {
            if (rule == null)
                throw new ArgumentException("Interpretation requires a Process-rule instance.", nameof(rule));

            return InterpretDeclared(rule.InstanceID, rule.Definition.ID.Value, rule.Definition.Effects, null);
        }

        /// <summary>
        /// Interprets every EXECUTION-domain effect the host's attached Patches declare, socketing each
        /// produced effect to the host. The effect origin is the Patch instance and the definition
        /// identity the Patch definition, so two attachments of one definition stay distinct origins.
        /// Attachments resolve in socket order, which is the order the host stores them in.
        /// </summary>
        /// <param name="host">The Instruction instance whose sockets are read.</param>
        /// <returns>The interpreted effects; empty when the host carries no Patch.</returns>
        /// <exception cref="ArgumentException">Thrown when the host is null or an EXECUTION effect is uninterpretable.</exception>
        public static IReadOnlyList<ActiveEffect> Interpret(InstructionInstance host)
        {
            if (host == null)
                throw new ArgumentException("Interpretation requires an Instruction instance.", nameof(host));

            if (host.AttachedPatches.Count == 0)
                return new List<ActiveEffect>();

            List<ActiveEffect> effects = new List<ActiveEffect>();
            for (int i = 0; i < host.AttachedPatches.Count; i++)
            {
                PatchInstance attached = host.AttachedPatches[i].Patch;
                IReadOnlyList<ActiveEffect> interpreted = InterpretDeclared(
                    attached.InstanceID,
                    attached.Definition.ID.Value,
                    attached.Definition.Effects,
                    host.InstanceID);

                for (int j = 0; j < interpreted.Count; j++)
                {
                    effects.Add(interpreted[j]);
                }
            }

            return effects;
        }

        /// <summary>
        /// Interprets a declared effect list against the closed vocabulary under one owning origin,
        /// socketing every produced effect when a host instance is supplied.
        /// </summary>
        /// <param name="origin">The owning content instance's identity.</param>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="declared">The declared effects.</param>
        /// <param name="hostInstance">The socketed host instance, or null for unsocketed content.</param>
        /// <returns>The interpreted effects in declaration order.</returns>
        /// <exception cref="ArgumentException">Thrown when an EXECUTION effect is uninterpretable.</exception>
        private static IReadOnlyList<ActiveEffect> InterpretDeclared(
            InstanceID origin,
            string definitionID,
            IReadOnlyList<EffectDefinition> declared,
            InstanceID? hostInstance
        )
        {
            List<ActiveEffect> effects = new List<ActiveEffect>(declared.Count);
            for (int i = 0; i < declared.Count; i++)
            {
                EffectDefinition effect = declared[i];
                if (effect.PhaseDomain != PhaseDomain.Execution)
                    continue;

                ActiveEffect interpreted = InterpretExecutionEffect(origin, definitionID, i, effect, hostInstance.HasValue);
                effects.Add(hostInstance.HasValue ? interpreted.WithHostInstance(hostInstance.Value) : interpreted);
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
        /// <param name="socketed">Whether the effect is declared by a Patch attached to a host.</param>
        /// <returns>The interpreted effect.</returns>
        /// <exception cref="ArgumentException">Thrown when any token falls outside the closed vocabulary.</exception>
        private static ActiveEffect InterpretExecutionEffect(
            InstanceID origin,
            string definitionID,
            int effectIndex,
            EffectDefinition effect,
            bool socketed
        )
        {
            TriggerDescriptor trigger = effect.Trigger;
            if (trigger == null)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries no trigger.");

            EffectFrequency frequency = RequireKnownFrequency(definitionID, effect.Frequency);
            string subtype = trigger.EventSubtype;

            if (trigger.EventFamily == EventFamily.Operation && subtype == ExecutionEventSubtypes.PrimaryOperationPending)
            {
                if (effect.Operation is CounterRequestOperation counterRequest)
                {
                    RequireUnsocketed(definitionID, socketed, "a Process-counter intervention");
                    RequireBand(definitionID, trigger, PreOperationBand);
                    RequireQuantityVocabularyQualifiers(definitionID, trigger, socketed);
                    return ActiveEffect.ForCounterIntervention(origin, definitionID, effectIndex, trigger, counterRequest, frequency);
                }
                
                RequireBand(definitionID, trigger, ModificationBand);
                RequireQuantityVocabularyQualifiers(definitionID, trigger, socketed);
                if (socketed)
                    RequireOwnHostTargeting(definitionID, effect, "modification");

                if (effect.Operation is OperationModificationOperation operandChange)
                {
                    if (!socketed)
                        throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' declares an OperationModification operation without a host socket; only an attached Patch may declare it.");

                    return ActiveEffect.ForOperationModification(origin, definitionID, effectIndex, trigger, operandChange, frequency);
                }

                return ActiveEffect.ForModification(origin, definitionID, effectIndex, trigger, RequireConstantQuantityChange(definitionID, effect.Operation), frequency);
            }

            if (trigger.EventFamily == EventFamily.Operation && subtype == ExecutionEventSubtypes.PrimaryOperationResolved)
            {
                RequireBand(definitionID, trigger, ReactionBand);
                RequireQuantityVocabularyQualifiers(definitionID, trigger, socketed);
                return ActiveEffect.ForReaction(origin, definitionID, effectIndex, trigger, RequireConstantQuantityChange(definitionID, effect.Operation), frequency);
            }

            if (trigger.EventFamily == EventFamily.Quantity && subtype == ExecutionEventSubtypes.QuantityChanged)
            {
                RequireBand(definitionID, trigger, ReactionBand);
                RequireQuantityVocabularyQualifiers(definitionID, trigger, socketed);
                if (effect.Operation is TargetLockUpdateOperation lockUpdate)
                {
                    if (socketed)
                        throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' declares a TargetLockUpdate operation on a host-socketed Patch; only Dependencies and Directives may declare it.");

                    if (lockUpdate.Selection.Kind != MostRecentQualifyingUnitSelection)
                        throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' declares target-lock selection '{lockUpdate.Selection.Kind}'; only '{MostRecentQualifyingUnitSelection}' is interpretable.");

                    return ActiveEffect.ForTargetLock(origin, definitionID, effectIndex, trigger, lockUpdate, frequency);
                }

                if (effect.Operation is AddedExecutionRequestOperation)
                {
                    AddedExecutionRequestOperation request = RequireAddedExecutionRequest(definitionID, effect.Operation, TriggeringUnitTargeting);
                    return ActiveEffect.ForAddedExecution(origin, definitionID, effectIndex, trigger, request, frequency);
                }

                if (effect.Operation is CounterRequestOperation coolingRequest)
                {
                    RequireUnsocketed(definitionID, socketed, "a Process-counter reaction");
                    return ActiveEffect.ForCounterReaction(origin, definitionID, effectIndex, trigger, coolingRequest, frequency);
                }
                
                if (socketed)
                    RequireHostInstructionQualifier(definitionID, trigger);

                return ActiveEffect.ForReaction(origin, definitionID, effectIndex, trigger, RequireConstantQuantityChange(definitionID, effect.Operation), frequency);
            }

            if (trigger.EventFamily == EventFamily.Disposition && subtype == ExecutionEventSubtypes.SourceExecutionSkipped)
            {
                RequireUnsocketed(definitionID, socketed, "the rescue trigger pair");
                RequireBand(definitionID, trigger, PreOperationBand);
                RequireNoQualifiers(definitionID, trigger, "the rescue trigger pair");
                return ActiveEffect.ForRescue(origin, definitionID, effectIndex, trigger, RequireRescueOperation(definitionID, effect.Operation), frequency);
            }

            if (trigger.EventFamily == EventFamily.Lifecycle && subtype == ExecutionEventSubtypes.RuntimeUnitCompleted)
            {
                RequireBand(definitionID, trigger, PostUnitBand);
                RequirePostUnitQualifiers(definitionID, trigger, socketed);
                if (socketed)
                    RequireOwnHostTargeting(definitionID, effect, "added-execution creator");

                string requiredTargeting = socketed ? OwnHostTargeting : TriggeringUnitTargeting;
                AddedExecutionRequestOperation request = RequireAddedExecutionRequest(definitionID, effect.Operation, requiredTargeting);
                return ActiveEffect.ForAddedExecution(origin, definitionID, effectIndex, trigger, request, frequency);
            }

            if (trigger.EventFamily == EventFamily.Structure && subtype == ExecutionEventSubtypes.ConditionTrue)
            {
                RequireUnsocketed(definitionID, socketed, "the CONDITION_TRUE trigger pair");
                RequireBand(definitionID, trigger, PostUnitBand);
                RequireNoQualifiers(definitionID, trigger, "the CONDITION_TRUE trigger pair");
                AddedExecutionRequestOperation request = RequireAddedExecutionRequest(definitionID, effect.Operation, FirstContainedInstructionTargeting);
                return ActiveEffect.ForAddedExecution(origin, definitionID, effectIndex, trigger, request, frequency);
            }

            if (trigger.EventFamily == EventFamily.Reaction && subtype == ExecutionEventSubtypes.BoundaryEffectRequested)
            {
                RequireUnsocketed(definitionID, socketed, "the boundary trigger pair");
                string boundaryName = RequireWiredBoundary(definitionID, trigger);
                RequireBoundaryQualifiers(definitionID, trigger);
                if (effect.Operation is AddedExecutionRequestOperation)
                {
                    AddedExecutionRequestOperation request = RequireBoundaryCreatorRequest(definitionID, effect.Operation);
                    return ActiveEffect.ForBoundaryCreator(origin, definitionID, effectIndex, trigger, request, boundaryName, frequency);
                }

                return ActiveEffect.ForBoundary(origin, definitionID, effectIndex, trigger, RequireConstantQuantityChange(definitionID, effect.Operation), boundaryName, frequency);
            }

            throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries an unsupported trigger pair '{trigger.EventFamily}/{subtype}'.");
        }

        /// <summary>
        /// Validates that a trigger pair no Patch may declare was not declared by one.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="socketed">Whether the effect is host-socketed.</param>
        /// <param name="pairDescription">The pair named in the failure message.</param>
        /// <exception cref="ArgumentException">Thrown when the effect is host-socketed.</exception>
        private static void RequireUnsocketed(
            string definitionID,
            bool socketed,
            string pairDescription
        )
        {
            if (socketed)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' declares {pairDescription} on a host-socketed Patch; the pair is not interpretable from a Patch.");
        }

        /// <summary>
        /// Validates that a host-socketed shape declares its host-locality through own-host targeting
        /// rather than leaving it to be inferred from the effect's Patch provenance.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="effect">The declared effect.</param>
        /// <param name="shapeDescription">The shape named in the failure message.</param>
        /// <exception cref="ArgumentException">Thrown when the effect declares no own-host targeting.</exception>
        private static void RequireOwnHostTargeting(
            string definitionID,
            EffectDefinition effect,
            string shapeDescription
        )
        {
            string declared = effect.Targeting == null ? "none" : effect.Targeting.Kind;
            if (declared != OwnHostTargeting)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' declares a host-socketed {shapeDescription} whose targeting '{declared}' is not host-referential; socketed shapes of this kind require '{OwnHostTargeting}' targeting.");
        }

        /// <summary>
        /// Validates that a host-socketed quantity reaction declares its host-locality through the
        /// host-Instruction operation-class qualifier.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="trigger">The trigger descriptor.</param>
        /// <exception cref="ArgumentException">Thrown when the qualifier is absent.</exception>
        private static void RequireHostInstructionQualifier(string definitionID, TriggerDescriptor trigger)
        {
            for (int i = 0; i < trigger.Qualifiers.Count; i++)
            {
                TriggerQualifier qualifier = trigger.Qualifiers[i];
                if (qualifier.Kind == "OPERATION_CLASS" && qualifier.Value == HostInstructionQualifier)
                    return;
            }

            throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' declares a host-socketed quantity reaction with no host-referential declaration; socketed quantity reactions require the 'OPERATION_CLASS'/'{HostInstructionQualifier}' qualifier.");
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
        /// by the pending-operation, resolved-operation, and quantity-change pairs. The host-Instruction
        /// operation class is admitted only for host-socketed effects.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="trigger">The trigger descriptor.</param>
        /// <param name="socketed">Whether the effect is declared by a Patch attached to a host.</param>
        /// <exception cref="ArgumentException">Thrown when any qualifier is outside the closed vocabulary.</exception>
        private static void RequireQuantityVocabularyQualifiers(
            string definitionID,
            TriggerDescriptor trigger,
            bool socketed
        )
        {
            for (int i = 0; i < trigger.Qualifiers.Count; i++)
            {
                TriggerQualifier qualifier = trigger.Qualifiers[i];
                bool known;
                switch (qualifier.Kind)
                {
                    case "OPERATION_CLASS":
                        known = qualifier.Value is "FIXED_ADDITION" or "PLAYER_INSTRUCTION" or "VALUE_ADD_SIGNAL" or "MULTIPLY" || (socketed && qualifier.Value == HostInstructionQualifier);
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
        /// Validates the post-unit pair's qualifiers per value: the Repeat-context qualifier is
        /// evaluable only for unsocketed content, and the four host-context qualifiers only for a
        /// host-socketed Patch.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="trigger">The trigger descriptor.</param>
        /// <param name="socketed">Whether the effect is declared by a Patch attached to a host.</param>
        /// <exception cref="ArgumentException">Thrown when any qualifier is outside the pair's vocabulary.</exception>
        private static void RequirePostUnitQualifiers(
            string definitionID,
            TriggerDescriptor trigger,
            bool socketed
        )
        {
            for (int i = 0; i < trigger.Qualifiers.Count; i++)
            {
                TriggerQualifier qualifier = trigger.Qualifiers[i];
                bool known;
                if (qualifier.Kind == "STRUCTURE_CONTEXT" && qualifier.Value == "INSIDE_REPEAT")
                    known = !socketed;
                else if (qualifier.Kind == "POSITIONAL" && qualifier.Value is "EVEN_NUMBERED_LINE" or "FINAL_OCCUPIED_PLAYER_LINE")
                    known = socketed;
                else if (qualifier.Kind == "STRUCTURE_CONTEXT" && qualifier.Value is "INSIDE_SUCCEEDING_CONDITION" or "ADJACENT_AFTER_SUCCESSFUL_SCORE")
                    known = socketed;
                else
                    known = false;

                if (!known)
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
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' declares CancelOnInvalid = true; only false is interpretable on this trigger pair.");

            return request;
        }

        /// <summary>
        /// Validates that the boundary pair's added-execution request targets the creator's target
        /// lock and cancels on an invalid host — the only shape at which cancel-on-invalid is
        /// interpretable.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="operation">The declared operation.</param>
        /// <returns>The operation as an added-execution request.</returns>
        /// <exception cref="ArgumentException">Thrown when the targeting or cancel flag is unsupported.</exception>
        private static AddedExecutionRequestOperation RequireBoundaryCreatorRequest(string definitionID, EffectOperation operation)
        {
            AddedExecutionRequestOperation request = (AddedExecutionRequestOperation)operation;
            if (request.Target.Kind != LockedTargetTargeting)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' declares added-execution targeting '{request.Target.Kind}'; only '{LockedTargetTargeting}' is interpretable on the boundary trigger pair.");

            if (!request.CancelOnInvalid)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' declares CancelOnInvalid = false against '{LockedTargetTargeting}'; a locked-target request must cancel on an invalid host.");

            return request;
        }

        /// <summary>
        /// Validates that the frequency is present with an allowance and a reset scope from the closed
        /// sets.
        /// </summary>
        /// <param name="definitionID">The owning definition's identity.</param>
        /// <param name="frequency">The declared frequency.</param>
        /// <returns>The frequency unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the frequency is absent or its allowance or scope unsupported.</exception>
        private static EffectFrequency RequireKnownFrequency(string definitionID, EffectFrequency frequency)
        {
            if (frequency == null)
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries no frequency.");

            if (frequency.Allowance != "FIRST_QUALIFYING_EVENT" && frequency.Allowance != "EVERY_QUALIFYING_EVENT" && frequency.Allowance != "ONCE")
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries an unsupported allowance '{frequency.Allowance}'.");

            if (frequency.Scope != "EXECUTION" && frequency.Scope != "DECLARED_SCOPE" && frequency.Scope != "SOURCE_EXECUTION")
                throw new ArgumentException($"An EXECUTION-domain effect of '{definitionID}' carries an unsupported reset scope '{frequency.Scope}'.");

            return frequency;
        }
    }
}