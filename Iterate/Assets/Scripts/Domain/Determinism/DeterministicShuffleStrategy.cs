using System.Collections.Generic;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// Deterministic shuffle: one complete reproducible permutation of the captured set via
    /// Fisher–Yates. The decision has one draw record (ordinal 1) whose populations are the captured set in
    /// canonical order, unchanged — nothing is consumed by an ordering; the permutation lives in the
    /// record's result permutation, and the outcome is the full permutation.
    /// </summary>
    internal sealed class DeterministicShuffleStrategy : ISelectionStrategy
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

            return new DecisionOutcome(DecisionDisposition.Selected, new List<string>(permuted));
        }
    }
}