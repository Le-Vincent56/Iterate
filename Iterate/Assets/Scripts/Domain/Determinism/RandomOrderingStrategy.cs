using System.Collections.Generic;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// Random ordering of a captured finite set: the same Fisher–Yates permutation as a
    /// shuffle, but the outcome is the first declared-count identities of the permutation. The full
    /// permutation is retained in the record's result permutation as reproduction evidence; the declared
    /// unconsumed-suffix disposition travels in the request echo. One draw record, populations unchanged.
    /// </summary>
    internal sealed class RandomOrderingStrategy : ISelectionStrategy
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
            List<string> canonical = SelectionSupport.Identities(snapshot.Candidates);
            List<string> permuted = SelectionSupport.FisherYates(canonical, stream);
            for (int index = 0; index < permuted.Count; index++)
            {
                resultPermutation.Add(permuted[index]);
            }

            int take = effectiveCount < permuted.Count ? effectiveCount : permuted.Count;
            List<string> prefix = new List<string>(take);
            for (int index = 0; index < take; index++)
            {
                prefix.Add(permuted[index]);
            }

            if (permuted.Count > 0)
            {
                draws.Add(new DrawRecord(
                    1,
                    new List<string>(canonical),
                    permuted[0],
                    ReplacementBehavior.RemainsEligible,
                    new List<string>(canonical)
                ));
            }

            return new DecisionOutcome(DecisionDisposition.Selected, prefix);
        }
    }
}