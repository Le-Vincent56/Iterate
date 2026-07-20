using System;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// A captured candidate: its stable identity, its canonical ordering key, and its final selection
    /// weight when weighted. <see cref="Identity"/> is non-empty and <see cref="OrderingKey"/> non-null;
    /// <see cref="FinalWeight"/> is null for unweighted decisions or a captured value of zero or greater —
    /// a negative weight is a data defect and throws.
    /// </summary>
    /// <param name="Identity">The stable candidate identity; non-empty.</param>
    /// <param name="OrderingKey">The canonical ordering key; non-null.</param>
    /// <param name="FinalWeight">The exact final weight, or null when unweighted.</param>
    public sealed record CandidateEntry(string Identity, CandidateOrderingKey OrderingKey, int? FinalWeight)
    {
        /// <summary>
        /// The stable candidate identity; non-empty.
        /// </summary>
        public string Identity { get; } = RequireNonEmpty(Identity, nameof(Identity));

        /// <summary>
        /// The canonical ordering key; non-null.
        /// </summary>
        public CandidateOrderingKey OrderingKey { get; } = RequireKey(OrderingKey, nameof(OrderingKey));

        /// <summary>
        /// The exact final weight, or null when unweighted; never negative.
        /// </summary>
        public int? FinalWeight { get; } = RequireNonNegativeOrAbsent(FinalWeight, nameof(FinalWeight));

        /// <summary>
        /// Validates the identity is present and non-empty, returning it unchanged.
        /// </summary>
        /// <param name="value">The identity value.</param>
        /// <param name="fieldName">The field name reported on failure.</param>
        /// <returns>The value unchanged.</returns>
        private static string RequireNonEmpty(string value, string fieldName)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("A candidate entry requires a non-empty identity.", fieldName);

            return value;
        }

        /// <summary>
        /// Validates the ordering key is non-null, returning it unchanged.
        /// </summary>
        /// <param name="value">The ordering key.</param>
        /// <param name="fieldName">The field name reported on failure.</param>
        /// <returns>The value unchanged.</returns>
        private static CandidateOrderingKey RequireKey(CandidateOrderingKey value, string fieldName)
        {
            if (value == null)
                throw new ArgumentException("A candidate entry requires an ordering key.", fieldName);

            return value;
        }

        /// <summary>
        /// Validates the weight is null or zero-or-greater, returning it unchanged.
        /// </summary>
        /// <param name="value">The final weight, or null when unweighted.</param>
        /// <param name="fieldName">The field name reported on failure.</param>
        /// <returns>The value unchanged.</returns>
        private static int? RequireNonNegativeOrAbsent(int? value, string fieldName)
        {
            if (value is < 0)
                throw new ArgumentException("A final weight must be zero or greater.", fieldName);

            return value;
        }
    }
}