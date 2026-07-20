using System.Collections.Generic;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// Uniform selection without replacement: each draw is equal-probability over the
    /// remaining candidates, and the selected candidate is removed for the next draw. Each draw records the
    /// shrinking population before and after and the declared removal consequence.
    /// </summary>
    internal sealed class UniformSelectionWithoutReplacementStrategy : ISelectionStrategy
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
            List<string> working = SelectionSupport.Identities(snapshot.Candidates);
            List<string> selectedIdentities = new List<string>(effectiveCount);
            for (int drawIndex = 0; drawIndex < effectiveCount; drawIndex++)
            {
                List<string> before = new List<string>(working);
                uint pick = stream.NextUInt32((uint)working.Count);
                string selected = working[(int)pick];
                working.RemoveAt((int)pick);
                List<string> after = new List<string>(working);
                draws.Add(new DrawRecord(drawIndex + 1, before, selected, request.Replacement, after));
                selectedIdentities.Add(selected);
            }

            return new DecisionOutcome(DecisionDisposition.Selected, selectedIdentities);
        }
    }
}