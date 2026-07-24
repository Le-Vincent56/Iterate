using System;
using System.Globalization;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One registered, interpretable EXECUTION effect as a validated tagged union constructed only
    /// through its five per-kind static factories: the owning content instance, the definition
    /// identity and effect index, the interpreted trigger, the participation kind, and exactly one
    /// operation — a constant-operand quantity change for the modification, reaction, and boundary
    /// kinds, a rescue operation for the rescue kind, an added-execution request operation for the
    /// added-execution kind. Invalid kind/operation combinations are unrepresentable at call sites.
    /// </summary>
    public sealed record ActiveEffect
    {
        /// <summary>
        /// The owning content instance's identity.
        /// </summary>
        public InstanceID Origin { get; }

        /// <summary>
        /// The owning definition's surrogate-key identity.
        /// </summary>
        public string DefinitionID { get; }

        /// <summary>
        /// The effect's index within the definition's effect list.
        /// </summary>
        public int EffectIndex { get; }

        /// <summary>
        /// The interpreted trigger descriptor.
        /// </summary>
        public TriggerDescriptor Trigger { get; }

        /// <summary>
        /// How the effect participates at the engine's boundaries.
        /// </summary>
        public ActiveEffectKind Kind { get; }

        /// <summary>
        /// The quantity-change operation; null except for the modification, reaction, and boundary
        /// kinds.
        /// </summary>
        public QuantityChangeOperation Operation { get; }

        /// <summary>
        /// The rescue operation; null except for the rescue kind.
        /// </summary>
        public RescueOperation Rescue { get; }

        /// <summary>
        /// The added-execution request operation; null except for the added-execution kind.
        /// </summary>
        public AddedExecutionRequestOperation Request { get; }

        /// <summary>
        /// The named scheduling boundary the effect resolves at; null except for the boundary kind.
        /// </summary>
        public string BoundaryName { get; }

        /// <summary>
        /// The effect's frequency declaration.
        /// </summary>
        public EffectFrequency Frequency { get; }

        /// <summary>
        /// Whether the effect transforms the pending operation at the modification band. Derived
        /// from the kind.
        /// </summary>
        public bool IsModification { get; }

        /// <summary>
        /// The per-source-instance frequency-ledger key, composed once at construction as
        /// definition identity, effect index, and owning instance value.
        /// </summary>
        public string FrequencyKey { get; }

        private ActiveEffect(
            InstanceID origin,
            string definitionID,
            int effectIndex,
            TriggerDescriptor trigger,
            ActiveEffectKind kind,
            QuantityChangeOperation operation,
            RescueOperation rescue,
            AddedExecutionRequestOperation request,
            string boundaryName,
            EffectFrequency frequency
        )
        {
            Origin = origin;
            DefinitionID = definitionID;
            EffectIndex = effectIndex;
            Trigger = trigger;
            Kind = kind;
            Operation = operation;
            Rescue = rescue;
            Request = request;
            BoundaryName = boundaryName;
            Frequency = frequency;
            IsModification = kind == ActiveEffectKind.Modification;
            FrequencyKey = definitionID + ":" + effectIndex.ToString(CultureInfo.InvariantCulture) + "#" + origin.Value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Creates a modification-kind effect transforming the pending primary operation.
        /// </summary>
        /// <param name="origin">The owning content instance's identity.</param>
        /// <param name="definitionID">The owning definition's surrogate-key identity.</param>
        /// <param name="effectIndex">The effect's index within the definition.</param>
        /// <param name="trigger">The interpreted trigger descriptor.</param>
        /// <param name="operation">The constant-operand quantity change.</param>
        /// <param name="frequency">The effect's frequency declaration.</param>
        /// <returns>The validated effect.</returns>
        /// <exception cref="ArgumentException">Thrown when a required component is missing.</exception>
        public static ActiveEffect ForModification(
            InstanceID origin,
            string definitionID,
            int effectIndex,
            TriggerDescriptor trigger,
            QuantityChangeOperation operation,
            EffectFrequency frequency
        )
        {
            RequireCommon(definitionID, trigger, frequency);
            if (operation == null)
                throw new ArgumentException("A modification-kind ActiveEffect requires a quantity change.", nameof(operation));

            return new ActiveEffect(origin, definitionID, effectIndex, trigger, ActiveEffectKind.Modification, operation, null, null, null, frequency);
        }

        /// <summary>
        /// Creates a reaction-kind effect resolving a quantity change at the immediate-reaction band.
        /// </summary>
        /// <param name="origin">The owning content instance's identity.</param>
        /// <param name="definitionID">The owning definition's surrogate-key identity.</param>
        /// <param name="effectIndex">The effect's index within the definition.</param>
        /// <param name="trigger">The interpreted trigger descriptor.</param>
        /// <param name="operation">The constant-operand quantity change.</param>
        /// <param name="frequency">The effect's frequency declaration.</param>
        /// <returns>The validated effect.</returns>
        /// <exception cref="ArgumentException">Thrown when a required component is missing.</exception>
        public static ActiveEffect ForReaction(
            InstanceID origin,
            string definitionID,
            int effectIndex,
            TriggerDescriptor trigger,
            QuantityChangeOperation operation,
            EffectFrequency frequency
        )
        {
            RequireCommon(definitionID, trigger, frequency);
            if (operation == null)
                throw new ArgumentException("A reaction-kind ActiveEffect requires a quantity change.", nameof(operation));

            return new ActiveEffect(origin, definitionID, effectIndex, trigger, ActiveEffectKind.Reaction, operation, null, null, null, frequency);
        }

        /// <summary>
        /// Creates a rescue-kind effect transforming a skipped source execution.
        /// </summary>
        /// <param name="origin">The owning content instance's identity.</param>
        /// <param name="definitionID">The owning definition's surrogate-key identity.</param>
        /// <param name="effectIndex">The effect's index within the definition.</param>
        /// <param name="trigger">The interpreted trigger descriptor.</param>
        /// <param name="rescue">The rescue operation.</param>
        /// <param name="frequency">The effect's frequency declaration.</param>
        /// <returns>The validated effect.</returns>
        /// <exception cref="ArgumentException">Thrown when a required component is missing.</exception>
        public static ActiveEffect ForRescue(
            InstanceID origin,
            string definitionID,
            int effectIndex,
            TriggerDescriptor trigger,
            RescueOperation rescue,
            EffectFrequency frequency
        )
        {
            RequireCommon(definitionID, trigger, frequency);
            if (rescue == null)
                throw new ArgumentException("A rescue-kind ActiveEffect requires a rescue operation.", nameof(rescue));

            return new ActiveEffect(origin, definitionID, effectIndex, trigger, ActiveEffectKind.Rescue, null, rescue, null, null, frequency);
        }

        /// <summary>
        /// Creates an added-execution-kind effect creating one request per qualifying occurrence.
        /// </summary>
        /// <param name="origin">The owning content instance's identity.</param>
        /// <param name="definitionID">The owning definition's surrogate-key identity.</param>
        /// <param name="effectIndex">The effect's index within the definition.</param>
        /// <param name="trigger">The interpreted trigger descriptor.</param>
        /// <param name="request">The added-execution request operation.</param>
        /// <param name="frequency">The effect's frequency declaration.</param>
        /// <returns>The validated effect.</returns>
        /// <exception cref="ArgumentException">Thrown when a required component is missing.</exception>
        public static ActiveEffect ForAddedExecution(
            InstanceID origin,
            string definitionID,
            int effectIndex,
            TriggerDescriptor trigger,
            AddedExecutionRequestOperation request,
            EffectFrequency frequency
        )
        {
            RequireCommon(definitionID, trigger, frequency);
            if (request == null)
                throw new ArgumentException("An added-execution-kind ActiveEffect requires a request operation.", nameof(request));

            return new ActiveEffect(origin, definitionID, effectIndex, trigger, ActiveEffectKind.AddedExecution, null, null, request, null, frequency);
        }

        /// <summary>
        /// Creates a boundary-kind effect resolving a source-less quantity change at a named
        /// traversal boundary.
        /// </summary>
        /// <param name="origin">The owning content instance's identity.</param>
        /// <param name="definitionID">The owning definition's surrogate-key identity.</param>
        /// <param name="effectIndex">The effect's index within the definition.</param>
        /// <param name="trigger">The interpreted trigger descriptor.</param>
        /// <param name="operation">The constant-operand quantity change.</param>
        /// <param name="boundaryName">The named scheduling boundary token.</param>
        /// <param name="frequency">The effect's frequency declaration.</param>
        /// <returns>The validated effect.</returns>
        /// <exception cref="ArgumentException">Thrown when a required component is missing.</exception>
        public static ActiveEffect ForBoundary(
            InstanceID origin,
            string definitionID,
            int effectIndex,
            TriggerDescriptor trigger,
            QuantityChangeOperation operation,
            string boundaryName,
            EffectFrequency frequency
        )
        {
            RequireCommon(definitionID, trigger, frequency);
            if (operation == null)
                throw new ArgumentException("A boundary-kind ActiveEffect requires a quantity change.", nameof(operation));

            if (string.IsNullOrEmpty(boundaryName))
                throw new ArgumentException("A boundary-kind ActiveEffect requires a boundary name.", nameof(boundaryName));

            return new ActiveEffect(origin, definitionID, effectIndex, trigger, ActiveEffectKind.Boundary, operation, null, null, boundaryName, frequency);
        }

        /// <summary>
        /// Validates the components every kind requires.
        /// </summary>
        /// <param name="definitionID">The candidate definition identity.</param>
        /// <param name="trigger">The candidate trigger descriptor.</param>
        /// <param name="frequency">The candidate frequency declaration.</param>
        /// <exception cref="ArgumentException">Thrown when a shared component is missing.</exception>
        private static void RequireCommon(
            string definitionID,
            TriggerDescriptor trigger,
            EffectFrequency frequency
        )
        {
            if (string.IsNullOrEmpty(definitionID))
                throw new ArgumentException("An ActiveEffect requires a definition identity.", nameof(definitionID));

            if (trigger == null)
                throw new ArgumentException("An ActiveEffect requires a trigger.", nameof(trigger));

            if (frequency == null)
                throw new ArgumentException("An ActiveEffect requires a frequency.", nameof(frequency));
        }
    }
}