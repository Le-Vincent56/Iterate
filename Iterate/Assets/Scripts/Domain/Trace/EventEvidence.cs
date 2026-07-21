using System;
using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Values;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The validated common core of one event record: its classification, ordering context, causal and
    /// containment linkage, source origin and ownership, source position, target, effect-origin lineage,
    /// added-execution context, structure context, disposition, safety status, and its optional grounded
    /// payload. Every field that is null means not-applicable; an empty string in a nullable string field
    /// is a defect and is rejected. Cross-field rules keep the record internally consistent: a
    /// disposition reason requires a disposition, player ownership requires a host instance, Core
    /// ownership requires a Core line identity, and an added-execution request identity is present
    /// exactly when the added-execution depth is one or greater. Equality is structural over the
    /// qualifier list; all other members compare by value.
    /// </summary>
    public sealed record EventEvidence
    {
        /// <summary>
        /// The controlled event family; non-empty and a member of the family registry.
        /// </summary>
        public string Family { get; }

        /// <summary>
        /// The family-specific subtype token; non-empty.
        /// </summary>
        public string Subtype { get; }

        /// <summary>
        /// The qualifier tokens refining the subtype, in declared order; never null, possibly empty, each
        /// entry non-empty.
        /// </summary>
        public IReadOnlyList<string> Qualifiers { get; }

        /// <summary>
        /// The causal nesting depth; zero or greater.
        /// </summary>
        public int CausalDepth { get; }

        /// <summary>
        /// The unit this event belongs to, or null when it belongs to none.
        /// </summary>
        public RuntimeUnitID? ContainingUnit { get; }

        /// <summary>
        /// The parent event in the containment tree, or null when this event has no parent.
        /// </summary>
        public TraceEventID? ParentEvent { get; }

        /// <summary>
        /// The event that caused this one, or null when uncaused.
        /// </summary>
        public TraceEventID? CausingEvent { get; }

        /// <summary>
        /// The player-owned source instance that hosts this event, or null when not player-owned.
        /// </summary>
        public InstanceID? HostInstance { get; }

        /// <summary>
        /// The Core-owned source line identity, or null when not Core-owned.
        /// </summary>
        public string CoreLineIdentity { get; }

        /// <summary>
        /// The instance that is the effect origin, or null when not applicable.
        /// </summary>
        public InstanceID? EffectOriginInstance { get; }

        /// <summary>
        /// The source-ownership classification, or null when this event has no source origin.
        /// </summary>
        public OwnershipClassification? Ownership { get; }

        /// <summary>
        /// The static source position, or null when this event has no source position.
        /// </summary>
        public SourcePosition? Position { get; }

        /// <summary>
        /// The identity of the effect target, or null when not applicable.
        /// </summary>
        public string TargetIdentity { get; }

        /// <summary>
        /// The effect-origin lineage; never null, the empty lineage for ordinary events.
        /// </summary>
        public EffectOriginLineage Lineage { get; }

        /// <summary>
        /// The added-execution request identity, or null when this event is not part of an added
        /// execution.
        /// </summary>
        public string AddedExecutionRequestIdentity { get; }

        /// <summary>
        /// The added-execution depth; zero when not part of an added execution, one or greater otherwise.
        /// </summary>
        public int AddedExecutionDepth { get; }

        /// <summary>
        /// The structure context, or null when this event executes outside any Structure.
        /// </summary>
        public StructureContext StructureContext { get; }

        /// <summary>
        /// The disposition of the occurrence, or null when this event records no disposition.
        /// </summary>
        public EventDisposition? Disposition { get; }

        /// <summary>
        /// The reason accompanying the disposition, or null when none; requires a disposition when present.
        /// </summary>
        public string DispositionReason { get; }

        /// <summary>
        /// The safety status at this event.
        /// </summary>
        public SafetyStatus SafetyStatus { get; }

        /// <summary>
        /// The grounded payload, or null when this event carries none.
        /// </summary>
        public EventPayload Payload { get; }

        public EventEvidence(
            string family,
            string subtype,
            IReadOnlyList<string> qualifiers,
            int causalDepth,
            RuntimeUnitID? containingUnit,
            TraceEventID? parentEvent,
            TraceEventID? causingEvent,
            InstanceID? hostInstance,
            string coreLineIdentity,
            InstanceID? effectOriginInstance,
            OwnershipClassification? ownership,
            SourcePosition? position,
            string targetIdentity,
            EffectOriginLineage lineage,
            string addedExecutionRequestIdentity,
            int addedExecutionDepth,
            StructureContext structureContext,
            EventDisposition? disposition,
            string dispositionReason,
            SafetyStatus safetyStatus,
            EventPayload payload
        )
        {
            if (string.IsNullOrEmpty(family))
                throw new ArgumentException("An event requires a family.", nameof(family));
            
            if (!EventFamilies.All.Contains(family))
                throw new ArgumentException("The event family is not a member of the registry.", nameof(family));
            
            if (string.IsNullOrEmpty(subtype))
                throw new ArgumentException("An event requires a subtype.", nameof(subtype));
            
            if (qualifiers == null)
                throw new ArgumentException("An event requires a qualifiers list.", nameof(qualifiers));
            
            for (int index = 0; index < qualifiers.Count; index++)
            {
                if (string.IsNullOrEmpty(qualifiers[index]))
                    throw new ArgumentException("A qualifier entry may not be empty.", nameof(qualifiers));
            }
            
            if (causalDepth < 0)
                throw new ArgumentException("Causal depth may not be negative.", nameof(causalDepth));
            
            if (coreLineIdentity is { Length: 0 })
                throw new ArgumentException("A Core line identity is null when not applicable; an empty string is invalid.", nameof(coreLineIdentity));
            
            if (targetIdentity is { Length: 0 })
                throw new ArgumentException("A target identity is null when not applicable; an empty string is invalid.", nameof(targetIdentity));
            
            if (addedExecutionRequestIdentity is { Length: 0 })
                throw new ArgumentException("An added-execution request identity is null when not applicable; an empty string is invalid.", nameof(addedExecutionRequestIdentity));
            
            if (dispositionReason is { Length: 0 })
                throw new ArgumentException("A disposition reason is null when not applicable; an empty string is invalid.", nameof(dispositionReason));
            
            if (addedExecutionDepth < 0)
                throw new ArgumentException("Added-execution depth may not be negative.", nameof(addedExecutionDepth));
            
            if (dispositionReason != null && disposition == null)
                throw new ArgumentException("A disposition reason requires a disposition.", nameof(dispositionReason));
            
            switch (ownership)
            {
                case OwnershipClassification.PlayerOwned when hostInstance == null:
                    throw new ArgumentException("Player-owned ownership requires a host instance.", nameof(hostInstance));
                
                case OwnershipClassification.CoreOwned when coreLineIdentity == null:
                    throw new ArgumentException("Core-owned ownership requires a Core line identity.", nameof(coreLineIdentity));
            }

            if (addedExecutionRequestIdentity != null && addedExecutionDepth < 1)
                throw new ArgumentException("An added-execution request identity requires a depth of at least one.", nameof(addedExecutionDepth));
            
            if (addedExecutionRequestIdentity == null && addedExecutionDepth != 0)
                throw new ArgumentException("A non-zero added-execution depth requires a request identity.", nameof(addedExecutionDepth));

            Family = family;
            Subtype = subtype;
            Qualifiers = qualifiers;
            CausalDepth = causalDepth;
            ContainingUnit = containingUnit;
            ParentEvent = parentEvent;
            CausingEvent = causingEvent;
            HostInstance = hostInstance;
            CoreLineIdentity = coreLineIdentity;
            EffectOriginInstance = effectOriginInstance;
            Ownership = ownership;
            Position = position;
            TargetIdentity = targetIdentity;
            Lineage = lineage ?? throw new ArgumentException("An event requires a lineage; use the empty lineage when none applies.", nameof(lineage));
            AddedExecutionRequestIdentity = addedExecutionRequestIdentity;
            AddedExecutionDepth = addedExecutionDepth;
            StructureContext = structureContext;
            Disposition = disposition;
            DispositionReason = dispositionReason;
            SafetyStatus = safetyStatus;
            Payload = payload;
        }

        /// <summary>
        /// Structural value equality: every scalar and value-typed field plus element-wise qualifier
        /// comparison. The lineage, structure context, and payload compare by their own value equality.
        /// </summary>
        /// <param name="other">The evidence to compare against.</param>
        /// <returns>True when every field is equal.</returns>
        public bool Equals(EventEvidence other)
        {
            if (other == null) return false;

            return string.Equals(Family, other.Family, StringComparison.Ordinal)
                && string.Equals(Subtype, other.Subtype, StringComparison.Ordinal)
                && CausalDepth == other.CausalDepth
                && ContainingUnit == other.ContainingUnit
                && ParentEvent == other.ParentEvent
                && CausingEvent == other.CausingEvent
                && HostInstance == other.HostInstance
                && string.Equals(CoreLineIdentity, other.CoreLineIdentity, StringComparison.Ordinal)
                && EffectOriginInstance == other.EffectOriginInstance
                && Ownership == other.Ownership
                && Position == other.Position
                && string.Equals(TargetIdentity, other.TargetIdentity, StringComparison.Ordinal)
                && Lineage == other.Lineage
                && string.Equals(AddedExecutionRequestIdentity, other.AddedExecutionRequestIdentity, StringComparison.Ordinal)
                && AddedExecutionDepth == other.AddedExecutionDepth
                && StructureContext == other.StructureContext
                && Disposition == other.Disposition
                && string.Equals(DispositionReason, other.DispositionReason, StringComparison.Ordinal)
                && SafetyStatus == other.SafetyStatus
                && Payload == other.Payload
                && TraceEquality.ListEquals(Qualifiers, other.Qualifiers
            );
        }

        /// <summary>
        /// A hash consistent with structural equality, over the family, subtype, causal depth, safety
        /// status, and qualifier count.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Family.GetHashCode();
                hash = hash * 31 + Subtype.GetHashCode();
                hash = hash * 31 + CausalDepth;
                hash = hash * 31 + (int)SafetyStatus;
                hash = hash * 31 + Qualifiers.Count;
                return hash;
            }
        }
    }
}