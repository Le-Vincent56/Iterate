using System;
using Iterate.Domain.Compilation;
using Iterate.Domain.Values;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The evidence captured when a runtime unit opens: its host identity and ownership, its source
    /// position, its activation kind and — for an added execution — its originating request, depth, and
    /// parent unit, plus its effect-origin lineage and structure context. Exactly one host identity holds
    /// per ownership. An added execution requires a request identity, a depth of at least one, and a
    /// parent unit; a canonical traversal carries none of the three. Equality is the synthesized value
    /// equality, which compares the lineage structurally.
    /// </summary>
    public sealed record RuntimeUnitOpening
    {
        /// <summary>
        /// The player-owned host instance, or null when the unit is Core-owned.
        /// </summary>
        public InstanceID? HostInstance { get; }

        /// <summary>
        /// The Core-owned source line identity, or null when the unit is player-owned.
        /// </summary>
        public string CoreLineIdentity { get; }

        /// <summary>
        /// The source-ownership classification of the unit.
        /// </summary>
        public OwnershipClassification Ownership { get; }

        /// <summary>
        /// The static source position, or null when the unit has none.
        /// </summary>
        public SourcePosition? Position { get; }

        /// <summary>
        /// How the unit was activated.
        /// </summary>
        public ActivationKind Activation { get; }

        /// <summary>
        /// The request that originated an added execution, or null for a canonical traversal.
        /// </summary>
        public string OriginatingRequestIdentity { get; }

        /// <summary>
        /// The added-execution depth; zero for a canonical traversal, one or greater for an added
        /// execution.
        /// </summary>
        public int AddedExecutionDepth { get; }

        /// <summary>
        /// The effect-origin lineage; never null, the empty lineage when none applies.
        /// </summary>
        public EffectOriginLineage Lineage { get; }

        /// <summary>
        /// The structure context, or null when the unit executes outside any Structure.
        /// </summary>
        public StructureContext StructureContext { get; }

        /// <summary>
        /// The creator unit of an added execution, or null for a canonical traversal.
        /// </summary>
        public RuntimeUnitID? ParentUnit { get; }

        public RuntimeUnitOpening(
            InstanceID? hostInstance,
            string coreLineIdentity,
            OwnershipClassification ownership,
            SourcePosition? position,
            ActivationKind activation,
            string originatingRequestIdentity,
            int addedExecutionDepth,
            EffectOriginLineage lineage,
            StructureContext structureContext,
            RuntimeUnitID? parentUnit
        )
        {
            if (coreLineIdentity is { Length: 0 })
                throw new ArgumentException("A Core line identity is null when not applicable; an empty string is invalid.", nameof(coreLineIdentity));
            
            if (originatingRequestIdentity is { Length: 0 })
                throw new ArgumentException("An originating request identity is null when not applicable; an empty string is invalid.", nameof(originatingRequestIdentity));
            
            if (lineage == null)
                throw new ArgumentException("A unit opening requires a lineage; use the empty lineage when none applies.", nameof(lineage));
            
            if (addedExecutionDepth < 0)
                throw new ArgumentException("Added-execution depth may not be negative.", nameof(addedExecutionDepth));

            if (ownership == OwnershipClassification.PlayerOwned)
            {
                if (hostInstance == null)
                    throw new ArgumentException("Player-owned ownership requires a host instance.", nameof(hostInstance));
                if (coreLineIdentity != null)
                    throw new ArgumentException("Player-owned ownership may not carry a Core line identity.", nameof(coreLineIdentity));
            }
            else
            {
                if (coreLineIdentity == null)
                    throw new ArgumentException("Core-owned ownership requires a Core line identity.", nameof(coreLineIdentity));
                if (hostInstance != null)
                    throw new ArgumentException("Core-owned ownership may not carry a host instance.", nameof(hostInstance));
            }

            if (activation == ActivationKind.AddedExecution)
            {
                if (originatingRequestIdentity == null)
                    throw new ArgumentException("An added execution requires an originating request identity.", nameof(originatingRequestIdentity));
                if (addedExecutionDepth < 1)
                    throw new ArgumentException("An added execution requires a depth of at least one.", nameof(addedExecutionDepth));
                if (parentUnit == null)
                    throw new ArgumentException("An added execution requires a parent unit.", nameof(parentUnit));
            }
            else
            {
                if (originatingRequestIdentity != null)
                    throw new ArgumentException("A canonical traversal may not carry an originating request identity.", nameof(originatingRequestIdentity));
                if (addedExecutionDepth != 0)
                    throw new ArgumentException("A canonical traversal requires a depth of zero.", nameof(addedExecutionDepth));
                if (parentUnit != null)
                    throw new ArgumentException("A canonical traversal may not carry a parent unit.", nameof(parentUnit));
            }

            HostInstance = hostInstance;
            CoreLineIdentity = coreLineIdentity;
            Ownership = ownership;
            Position = position;
            Activation = activation;
            OriginatingRequestIdentity = originatingRequestIdentity;
            AddedExecutionDepth = addedExecutionDepth;
            Lineage = lineage;
            StructureContext = structureContext;
            ParentUnit = parentUnit;
        }
    }
}