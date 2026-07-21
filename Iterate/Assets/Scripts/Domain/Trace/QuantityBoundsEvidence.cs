using System;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The bound applied while resolving a quantity change: an optional inclusive minimum and maximum.
    /// At least one bound must be present — a bounds record with neither bound is meaningless — and a
    /// present minimum may not exceed a present maximum.
    /// </summary>
    public sealed record QuantityBoundsEvidence
    {
        /// <summary>
        /// The inclusive lower bound, or null when no lower bound applied.
        /// </summary>
        public int? Minimum { get; }

        /// <summary>
        /// The inclusive upper bound, or null when no upper bound applied.
        /// </summary>
        public int? Maximum { get; }

        public QuantityBoundsEvidence(int? minimum, int? maximum)
        {
            if (minimum == null && maximum == null)
                throw new ArgumentException("A bounds evidence requires at least one bound.", nameof(minimum));
            
            if (minimum != null && maximum != null && minimum.Value > maximum.Value)
                throw new ArgumentException("A bounds minimum may not exceed its maximum.", nameof(minimum));

            Minimum = minimum;
            Maximum = maximum;
        }
    }
}