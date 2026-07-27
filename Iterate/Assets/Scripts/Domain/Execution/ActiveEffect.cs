using System;
using System.Globalization;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One registered, interpretable EXECUTION effect as a validated tagged union constructed only
    /// through its per-kind static factories: the owning content instance, the definition identity and
    /// effect index, the interpreted trigger, the participation kind, an optional host instance when
    /// the effect is socketed to one, and exactly one operation — a constant-operand quantity change
    /// for the modification, reaction, and boundary kinds, an operand adjustment for a host-socketed
    /// modification, a rescue operation for the rescue kind, an added-execution request operation for
    /// the added-execution kind, a target-lock update for the target-lock kind. Invalid
    /// kind/operation combinations are unrepresentable at call sites.
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
        /// The host instance the effect is socketed to; null unless the effect was interpreted from a
        /// Patch attached to that host.
        /// </summary>
        public InstanceID? HostInstance { get; }

        /// <summary>
        /// The quantity-change operation; null except for the modification, reaction, and boundary
        /// kinds.
        /// </summary>
        public QuantityChangeOperation Operation { get; }

        /// <summary>
        /// The operand adjustment applied to a host's pending operation; null except for a
        /// host-socketed modification.
        /// </summary>
        public OperationModificationOperation OperationModification { get; }

        /// <summary>
        /// The rescue operation; null except for the rescue kind.
        /// </summary>
        public RescueOperation Rescue { get; }

        /// <summary>
        /// The added-execution request operation; null except for the added-execution kind.
        /// </summary>
        public AddedExecutionRequestOperation Request { get; }

        /// <summary>
        /// The target-lock update operation; null except for the target-lock kind.
        /// </summary>
        public TargetLockUpdateOperation TargetLockUpdate { get; }

        /// <summary>
        /// The named scheduling boundary the effect resolves at; null except for the boundary kind
        /// and a boundary-scheduled added-execution creator.
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
            InstanceID? hostInstance,
            QuantityChangeOperation operation,
            OperationModificationOperation operationModification,
            RescueOperation rescue,
            AddedExecutionRequestOperation request,
            TargetLockUpdateOperation targetLockUpdate,
            string boundaryName,
            EffectFrequency frequency
        )
        {
            Origin = origin;
            DefinitionID = definitionID;
            EffectIndex = effectIndex;
            Trigger = trigger;
            Kind = kind;
            HostInstance = hostInstance;
            Operation = operation;
            OperationModification = operationModification;
            Rescue = rescue;
            Request = request;
            TargetLockUpdate = targetLockUpdate;
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

            return new ActiveEffect(origin, definitionID, effectIndex, trigger, ActiveEffectKind.Modification, null, operation, null, null, null, null, null, frequency);
        }

        /// <summary>
        /// Creates a modification-kind effect adjusting the host operation's fixed operand rather than
        /// contributing its own quantity change.
        /// </summary>
        /// <param name="origin">The owning content instance's identity.</param>
        /// <param name="definitionID">The owning definition's surrogate-key identity.</param>
        /// <param name="effectIndex">The effect's index within the definition.</param>
        /// <param name="trigger">The interpreted trigger descriptor.</param>
        /// <param name="operationModification">The operand adjustment.</param>
        /// <param name="frequency">The effect's frequency declaration.</param>
        /// <returns>The validated effect.</returns>
        /// <exception cref="ArgumentException">Thrown when a required component is missing.</exception>
        public static ActiveEffect ForOperationModification(
            InstanceID origin,
            string definitionID,
            int effectIndex,
            TriggerDescriptor trigger,
            OperationModificationOperation operationModification,
            EffectFrequency frequency
        )
        {
            RequireCommon(definitionID, trigger, frequency);
            if (operationModification == null)
                throw new ArgumentException("An operation-modification ActiveEffect requires an operand adjustment.", nameof(operationModification));

            return new ActiveEffect(origin, definitionID, effectIndex, trigger, ActiveEffectKind.Modification, null, null, operationModification, null, null, null, null, frequency);
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

            return new ActiveEffect(origin, definitionID, effectIndex, trigger, ActiveEffectKind.Reaction, null, operation, null, null, null, null, null, frequency);
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

            return new ActiveEffect(origin, definitionID, effectIndex, trigger, ActiveEffectKind.Rescue, null, null, null, rescue, null, null, null, frequency);
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

            return new ActiveEffect(
                origin,
                definitionID,
                effectIndex,
                trigger,
                ActiveEffectKind.AddedExecution,
                null,
                null,
                null,
                null,
                request,
                null,
                null,
                frequency
            );
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

            return new ActiveEffect(
                origin,
                definitionID,
                effectIndex,
                trigger,
                ActiveEffectKind.Boundary,
                null,
                operation,
                null,
                null,
                null,
                null,
                boundaryName,
                frequency
            );
        }
        
        /// <summary>
        /// Creates an added-execution-kind effect scheduled at a named traversal boundary rather than
        /// at a causal band, so boundary matching selects it by boundary name.
        /// </summary>
        /// <param name="origin">The owning content instance's identity.</param>
        /// <param name="definitionID">The owning definition's surrogate-key identity.</param>
        /// <param name="effectIndex">The effect's index within the definition.</param>
        /// <param name="trigger">The interpreted trigger descriptor.</param>
        /// <param name="request">The added-execution request operation.</param>
        /// <param name="boundaryName">The named scheduling boundary token.</param>
        /// <param name="frequency">The effect's frequency declaration.</param>
        /// <returns>The validated effect.</returns>
        /// <exception cref="ArgumentException">Thrown when a required component is missing.</exception>
        public static ActiveEffect ForBoundaryCreator(
            InstanceID origin,
            string definitionID,
            int effectIndex,
            TriggerDescriptor trigger,
            AddedExecutionRequestOperation request,
            string boundaryName,
            EffectFrequency frequency
        )
        {
            RequireCommon(definitionID, trigger, frequency);
            if (request == null)
                throw new ArgumentException("A boundary creator ActiveEffect requires a request operation.", nameof(request));

            if (string.IsNullOrEmpty(boundaryName))
                throw new ArgumentException("A boundary creator ActiveEffect requires a boundary name.", nameof(boundaryName));

            return new ActiveEffect(
                origin,
                definitionID,
                effectIndex,
                trigger,
                ActiveEffectKind.AddedExecution,
                null,
                null,
                null,
                null,
                request,
                null,
                boundaryName,
                frequency
            );
        }

        /// <summary>
        /// Creates a target-lock-kind effect updating its tracked target on each qualifying event and
        /// writing no register.
        /// </summary>
        /// <param name="origin">The owning content instance's identity.</param>
        /// <param name="definitionID">The owning definition's surrogate-key identity.</param>
        /// <param name="effectIndex">The effect's index within the definition.</param>
        /// <param name="trigger">The interpreted trigger descriptor.</param>
        /// <param name="targetLockUpdate">The target-lock update operation.</param>
        /// <param name="frequency">The effect's frequency declaration.</param>
        /// <returns>The validated effect.</returns>
        /// <exception cref="ArgumentException">Thrown when a required component is missing.</exception>
        public static ActiveEffect ForTargetLock(
            InstanceID origin,
            string definitionID,
            int effectIndex,
            TriggerDescriptor trigger,
            TargetLockUpdateOperation targetLockUpdate,
            EffectFrequency frequency
        )
        {
            RequireCommon(definitionID, trigger, frequency);
            if (targetLockUpdate == null)
                throw new ArgumentException("A target-lock-kind ActiveEffect requires a target-lock update.", nameof(targetLockUpdate));

            return new ActiveEffect(origin, definitionID, effectIndex, trigger, ActiveEffectKind.TargetLock, null, null, null, null, null, targetLockUpdate, null, frequency);
        }

        /// <summary>
        /// Derives the host-socketed form of this effect, carrying the instance it is attached to.
        /// Legal only for the kinds a Patch may declare at this content; the rescue, boundary, and
        /// target-lock kinds and boundary-scheduled creators refuse a host.
        /// </summary>
        /// <param name="hostInstance">The host instance the Patch is socketed to.</param>
        /// <returns>An equivalent effect carrying the host instance.</returns>
        /// <exception cref="ArgumentException">Thrown when the kind cannot be host-socketed.</exception>
        public ActiveEffect WithHostInstance(InstanceID hostInstance)
        {
            bool socketable = Kind == ActiveEffectKind.Modification
                || Kind == ActiveEffectKind.Reaction
                || (Kind == ActiveEffectKind.AddedExecution && BoundaryName == null);
            
            if (!socketable)
                throw new ArgumentException($"An ActiveEffect of kind '{Kind}' cannot be host-socketed.", nameof(hostInstance));

            return new ActiveEffect(
                Origin,
                DefinitionID,
                EffectIndex,
                Trigger,
                Kind,
                hostInstance,
                Operation,
                OperationModification,
                Rescue,
                Request,
                TargetLockUpdate,
                BoundaryName,
                Frequency);
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