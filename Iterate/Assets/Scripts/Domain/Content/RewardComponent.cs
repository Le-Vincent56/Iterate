using System;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// One reward component: the tier it is awarded at and exactly the payload its kind admits. An
    /// amount-bearing kind carries an amount and no reference; a content-bearing kind carries a
    /// reference and no amount. Whether the referenced ID resolves to the right category is the
    /// validator's check.
    /// </summary>
    /// <param name="Tier">The success tier this component is awarded at.</param>
    /// <param name="Kind">Which component kind this is.</param>
    /// <param name="Amount">The amount an amount-bearing kind awards; null otherwise.</param>
    /// <param name="Reference">The content or pool ID a content-bearing kind names; null otherwise.</param>
    public sealed record RewardComponent(
        RewardTier Tier,
        RewardComponentKind Kind,
        int? Amount,
        string Reference
    )
    {
        /// <summary>
        /// The amount an amount-bearing kind awards; null for a content-bearing kind. Validated
        /// against the component kind's whole field shape at construction.
        /// </summary>
        public int? Amount { get; } = RequireShape(Kind, Amount, Reference);

        /// <summary>
        /// Validates that a component carries exactly the payload its kind admits and returns the
        /// amount unchanged.
        /// </summary>
        /// <param name="kind">The declared component kind.</param>
        /// <param name="amount">The candidate amount.</param>
        /// <param name="reference">The candidate reference.</param>
        /// <returns>The amount unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the fields do not match the kind's shape.</exception>
        private static int? RequireShape(RewardComponentKind kind, int? amount, string reference)
        {
            if (CarriesAmount(kind))
            {
                if (!amount.HasValue)
                    throw new ArgumentException("An amount-bearing reward component requires an amount.", nameof(amount));

                if (!string.IsNullOrEmpty(reference))
                    throw new ArgumentException("An amount-bearing reward component must not carry a reference.", nameof(reference));

                return amount;
            }

            if (amount.HasValue)
                throw new ArgumentException("A content-bearing reward component must not carry an amount.", nameof(amount));

            if (string.IsNullOrEmpty(reference))
                throw new ArgumentException("A content-bearing reward component requires a reference.", nameof(reference));

            return amount;
        }

        /// <summary>
        /// Whether a component kind is awarded as an amount rather than as a content reference.
        /// </summary>
        /// <param name="kind">The component kind to classify.</param>
        /// <returns>True when the kind carries an amount.</returns>
        private static bool CarriesAmount(RewardComponentKind kind)
        {
            return kind is RewardComponentKind.Tokens 
                or RewardComponentKind.RouteTokenBonus 
                or RewardComponentKind.RAMSet;
        }
    }
}