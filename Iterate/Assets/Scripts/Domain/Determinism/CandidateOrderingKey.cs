using System;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// The canonical ordering key for a candidate: content-definition identity, then stable
    /// instance identity, then stable source/host/transaction identity, then an additional declared stable
    /// tie-break. <see cref="DefinitionIdentity"/> is required and non-empty; every other component is
    /// optional (null when absent) and never empty. <see cref="Compare"/> is the single comparator every
    /// snapshot orders by, so permutation-invariance is a property of this type.
    /// </summary>
    /// <param name="DefinitionIdentity">The canonical content-definition identity; required non-empty.</param>
    /// <param name="InstanceIdentity">The stable instance identity, or null when absent.</param>
    /// <param name="SourceIdentity">The stable source/host/transaction identity, or null when absent.</param>
    /// <param name="AdditionalTieBreak">An additional declared stable tie-break, or null when absent.</param>
    public sealed record CandidateOrderingKey(
        string DefinitionIdentity,
        string InstanceIdentity,
        string SourceIdentity,
        string AdditionalTieBreak
    )
    {
        /// <summary>
        /// The canonical content-definition identity; required and non-empty.
        /// </summary>
        public string DefinitionIdentity { get; } = RequireNonEmpty(DefinitionIdentity, nameof(DefinitionIdentity));

        /// <summary>
        /// The stable instance identity, or null when absent; never empty.
        /// </summary>
        public string InstanceIdentity { get; } = RequireAbsentOrNonEmpty(InstanceIdentity, nameof(InstanceIdentity));

        /// <summary>
        /// The stable source/host/transaction identity, or null when absent; never empty.
        /// </summary>
        public string SourceIdentity { get; } = RequireAbsentOrNonEmpty(SourceIdentity, nameof(SourceIdentity));

        /// <summary>
        /// An additional declared stable tie-break, or null when absent; never empty.
        /// </summary>
        public string AdditionalTieBreak { get; } = RequireAbsentOrNonEmpty(AdditionalTieBreak, nameof(AdditionalTieBreak));

        /// <summary>
        /// Compares two keys through the four components in canonical order using ordinal string
        /// comparison; an absent (null) component sorts before any present value. Returns zero only when
        /// the keys are fully identical.
        /// </summary>
        /// <param name="left">The left key.</param>
        /// <param name="right">The right key.</param>
        /// <returns>Negative, zero, or positive per the canonical order.</returns>
        public static int Compare(CandidateOrderingKey left, CandidateOrderingKey right)
        {
            int byDefinition = CompareComponent(left.DefinitionIdentity, right.DefinitionIdentity);
            if (byDefinition != 0)
                return byDefinition;

            int byInstance = CompareComponent(left.InstanceIdentity, right.InstanceIdentity);
            if (byInstance != 0)
                return byInstance;

            int bySource = CompareComponent(left.SourceIdentity, right.SourceIdentity);
            if (bySource != 0)
                return bySource;

            return CompareComponent(left.AdditionalTieBreak, right.AdditionalTieBreak);
        }

        /// <summary>
        /// Compares one optional component with null sorting before any value, else ordinal comparison.
        /// </summary>
        /// <param name="left">The left component, or null.</param>
        /// <param name="right">The right component, or null.</param>
        /// <returns>Negative, zero, or positive.</returns>
        private static int CompareComponent(string left, string right)
        {
            switch (left)
            {
                case null when right == null:
                    return 0;
                
                case null:
                    return -1;
            }

            return right == null ? 1 : string.CompareOrdinal(left, right);
        }

        /// <summary>
        /// Validates a required component is present and non-empty, returning it unchanged.
        /// </summary>
        /// <param name="value">The component value.</param>
        /// <param name="fieldName">The field name reported on failure.</param>
        /// <returns>The value unchanged.</returns>
        private static string RequireNonEmpty(string value, string fieldName)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException(
                    "A required ordering component must be non-empty.", 
                    fieldName
                );
            }

            return value;
        }

        /// <summary>
        /// Validates an optional component is null or non-empty (never empty), returning it unchanged.
        /// </summary>
        /// <param name="value">The component value, or null when absent.</param>
        /// <param name="fieldName">The field name reported on failure.</param>
        /// <returns>The value unchanged.</returns>
        private static string RequireAbsentOrNonEmpty(string value, string fieldName)
        {
            if (value is { Length: 0 })
            {
                throw new ArgumentException(
                    "An optional ordering component is null or non-empty, never empty.",
                    fieldName
                );
            }

            return value;
        }
    }
}