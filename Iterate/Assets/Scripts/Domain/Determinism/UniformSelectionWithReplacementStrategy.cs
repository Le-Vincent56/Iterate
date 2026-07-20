using System.Collections.Generic;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// Uniform selection with replacement: each draw is equal-probability over the full
    /// population, which never shrinks, so a candidate may repeat and the count may exceed the population.
    /// Every draw records the same population before and after and the remains-eligible consequence.
    /// </summary>
    internal sealed class UniformSelectionWithReplacementStrategy : ISelectionStrategy
    {
        /// <inheritdoc/>
        public DecisionOutcome Select(
            DecisionRequest request,
            CandidateSnapshot snapshot,
            Pcg32 stream,
            int effectiveCount,
            List<DrawRecord> draws,
            List<string> resultPermutation
        )
        {
            List<string> population = SelectionSupport.Identities(snapshot.Candidates);
            List<string> selectedIdentities = new List<string>(effectiveCount);
            for (int drawIndex = 0; drawIndex < effectiveCount; drawIndex++)
            {
                uint pick = stream.NextUInt32((uint)population.Count);
                string selected = population[(int)pick];
                List<string> before = new List<string>(population);
                List<string> after = new List<string>(population);
                draws.Add(new DrawRecord(drawIndex + 1, before, selected, ReplacementBehavior.RemainsEligible, after));
                selectedIdentities.Add(selected);
            }

            return new DecisionOutcome(DecisionDisposition.Selected, selectedIdentities);
        }
    }
}