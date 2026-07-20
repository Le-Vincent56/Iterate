using System;
using System.Collections.Generic;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// The resolved disposition of a decision and its selected identities in draw order —
    /// empty for a failed or cancelled decision, the permutation prefix for an ordering. Equality is
    /// structural over the selected identities.
    /// </summary>
    public sealed record DecisionOutcome
    {
        /// <summary>
        /// The resolved disposition.
        /// </summary>
        public DecisionDisposition Disposition { get; }

        /// <summary>
        /// The selected candidate identities in draw order; empty when nothing was selected.
        /// </summary>
        public IReadOnlyList<string> SelectedIdentities { get; }

        public DecisionOutcome(DecisionDisposition disposition, IReadOnlyList<string> selectedIdentities)
        {
            Disposition = disposition;
            SelectedIdentities = selectedIdentities ?? throw new ArgumentException("A decision outcome requires a selected-identities list.", nameof(selectedIdentities));
        }

        /// <summary>
        /// Structural value equality: disposition plus element-wise selected-identity comparison.
        /// </summary>
        /// <param name="other">The outcome to compare against.</param>
        /// <returns>True when the disposition and identities are equal.</returns>
        public bool Equals(DecisionOutcome other)
        {
            if (other == null) return false;

            return Disposition == other.Disposition && DeterminismEquality.ListEquals(SelectedIdentities, other.SelectedIdentities);
        }

        /// <summary>
        /// A hash consistent with structural equality, over the disposition and identity count.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)Disposition;
                hash = hash * 31 + SelectedIdentities.Count;
                return hash;
            }
        }
    }
}