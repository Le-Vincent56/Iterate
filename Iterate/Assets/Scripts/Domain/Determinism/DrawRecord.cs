using System;
using System.Collections.Generic;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// The record of a single draw: its one-based ordinal, the candidate population before
    /// the draw, the selected identity, the replacement consequence, and the population after — populations
    /// are candidate identities in canonical order. Equality is structural over the populations.
    /// </summary>
    public sealed record DrawRecord
    {
        /// <summary>
        /// The one-based draw ordinal within the decision.
        /// </summary>
        public int DrawOrdinal { get; }

        /// <summary>
        /// The candidate identities present before this draw, in canonical order.
        /// </summary>
        public IReadOnlyList<string> PopulationBefore { get; }

        /// <summary>
        /// The selected candidate identity.
        /// </summary>
        public string SelectedIdentity { get; }

        /// <summary>
        /// The replacement consequence applied to the selected candidate.
        /// </summary>
        public ReplacementBehavior Consequence { get; }

        /// <summary>
        /// The candidate identities present after this draw, in canonical order.
        /// </summary>
        public IReadOnlyList<string> PopulationAfter { get; }

        public DrawRecord(
            int drawOrdinal,
            IReadOnlyList<string> populationBefore,
            string selectedIdentity,
            ReplacementBehavior consequence,
            IReadOnlyList<string> populationAfter)
        {
            if (drawOrdinal < 1)
                throw new ArgumentException("A draw ordinal must be one or greater.", nameof(drawOrdinal));
            
            if (string.IsNullOrEmpty(selectedIdentity))
                throw new ArgumentException("A draw record requires a selected identity.", nameof(selectedIdentity));

            DrawOrdinal = drawOrdinal;
            PopulationBefore = populationBefore ?? throw new ArgumentException("A draw record requires a population-before list.", nameof(populationBefore));
            SelectedIdentity = selectedIdentity;
            Consequence = consequence;
            PopulationAfter = populationAfter ?? throw new ArgumentException("A draw record requires a population-after list.", nameof(populationAfter));
        }

        /// <summary>
        /// Structural value equality: scalar fields plus element-wise population comparison.
        /// </summary>
        /// <param name="other">The record to compare against.</param>
        /// <returns>True when every field is equal.</returns>
        public bool Equals(DrawRecord other)
        {
            if (other == null) return false;

            return DrawOrdinal == other.DrawOrdinal
                && Consequence == other.Consequence
                && string.Equals(SelectedIdentity, other.SelectedIdentity, StringComparison.Ordinal)
                && DeterminismEquality.ListEquals(PopulationBefore, other.PopulationBefore)
                && DeterminismEquality.ListEquals(PopulationAfter, other.PopulationAfter);
        }

        /// <summary>
        /// A hash consistent with structural equality, over scalar fields and population sizes only.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + DrawOrdinal;
                hash = hash * 31 + (int)Consequence;
                hash = hash * 31 + PopulationBefore.Count;
                hash = hash * 31 + PopulationAfter.Count;
                return hash;
            }
        }
    }
}