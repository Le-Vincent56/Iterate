using System.Collections.Generic;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// Uniform single selection: one draw at equal probability over the snapshot. The
    /// declared replacement is recorded as the draw consequence; the population-after reflects it.
    /// </summary>
    internal sealed class UniformSingleStrategy : ISelectionStrategy
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
            if (snapshot.Count < 1)
                return new DecisionOutcome(DecisionDisposition.Selected, new List<string>());

            List<string> before = SelectionSupport.Identities(snapshot.Candidates);
            uint pick = stream.NextUInt32((uint)snapshot.Count);
            string selected = snapshot.Candidates[(int)pick].Identity;

            List<string> after = new List<string>(before);
            if (request.Replacement != ReplacementBehavior.RemainsEligible)
                after.Remove(selected);

            draws.Add(new DrawRecord(1, before, selected, request.Replacement, after));
            return new DecisionOutcome(DecisionDisposition.Selected, new List<string> { selected });
        }
    }
}