using System;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The high-water safety tallies for one execution: the deepest added-execution lineage, the added
    /// descendant count, the source-execution unit count, the effect-reaction count, and the
    /// operation-transformation count. None may be negative.
    /// </summary>
    public readonly record struct SafetyCounts
    {
        /// <summary>
        /// The deepest added-execution lineage reached; zero or greater.
        /// </summary>
        public int LineageDepthHighWater { get; }

        /// <summary>
        /// The number of added descendants created; zero or greater.
        /// </summary>
        public int AddedDescendants { get; }

        /// <summary>
        /// The number of source-execution units; zero or greater.
        /// </summary>
        public int SourceExecutionUnits { get; }

        /// <summary>
        /// The number of effect reactions; zero or greater.
        /// </summary>
        public int EffectReactions { get; }

        /// <summary>
        /// The number of operation transformations; zero or greater.
        /// </summary>
        public int OperationTransformations { get; }

        public SafetyCounts(
            int lineageDepthHighWater,
            int addedDescendants,
            int sourceExecutionUnits,
            int effectReactions,
            int operationTransformations
        )
        {
            if (lineageDepthHighWater < 0)
                throw new ArgumentException("A safety count may not be negative.", nameof(lineageDepthHighWater));
            
            if (addedDescendants < 0)
                throw new ArgumentException("A safety count may not be negative.", nameof(addedDescendants));
            
            if (sourceExecutionUnits < 0)
                throw new ArgumentException("A safety count may not be negative.", nameof(sourceExecutionUnits));
            
            if (effectReactions < 0)
                throw new ArgumentException("A safety count may not be negative.", nameof(effectReactions));
            
            if (operationTransformations < 0)
                throw new ArgumentException("A safety count may not be negative.", nameof(operationTransformations));

            LineageDepthHighWater = lineageDepthHighWater;
            AddedDescendants = addedDescendants;
            SourceExecutionUnits = sourceExecutionUnits;
            EffectReactions = effectReactions;
            OperationTransformations = operationTransformations;
        }
    }
}