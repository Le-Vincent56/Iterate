using System;
using System.Collections.Generic;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// Weighted selection: each draw selects a candidate with probability proportional to its
    /// exact final weight, walking the canonical order and subtracting weights. Zero-weight candidates
    /// remain in evidence but are never selected; a zero total weight is routed to the
    /// declared insufficiency behavior by the service before dispatch. Without-replacement
    /// draws remove the selected candidate and re-total.
    /// </summary>
    internal sealed class WeightedSelectionStrategy : ISelectionStrategy
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
            List<CandidateEntry> working = new List<CandidateEntry>(snapshot.Candidates);
            List<string> selectedIdentities = new List<string>(effectiveCount);
            for (int drawIndex = 0; drawIndex < effectiveCount; drawIndex++)
            {
                ulong total = TotalWeight(working);
                if (total == 0UL)
                    break;
                if (total > uint.MaxValue)
                    throw new ArgumentException("The total weight exceeds the supported range.", nameof(snapshot));

                List<string> before = SelectionSupport.Identities(working);
                uint draw = stream.NextUInt32((uint)total);
                int selectedIndex = WalkToWeighted(working, draw);
                string selected = working[selectedIndex].Identity;

                List<string> after;
                if (request.Replacement == ReplacementBehavior.RemainsEligible)
                {
                    after = SelectionSupport.Identities(working);
                }
                else
                {
                    working.RemoveAt(selectedIndex);
                    after = SelectionSupport.Identities(working);
                }

                draws.Add(new DrawRecord(drawIndex + 1, before, selected, request.Replacement, after));
                selectedIdentities.Add(selected);
            }

            return new DecisionOutcome(DecisionDisposition.Selected, selectedIdentities);
        }

        /// <summary>
        /// Sums the final weights of the working candidates as a 64-bit total.
        /// </summary>
        /// <param name="candidates">The working candidates.</param>
        /// <returns>The total weight.</returns>
        private static ulong TotalWeight(List<CandidateEntry> candidates)
        {
            ulong total = 0UL;
            for (int index = 0; index < candidates.Count; index++)
            {
                CandidateEntry entry = candidates[index];
                if (entry.FinalWeight.HasValue)
                    total += (ulong)entry.FinalWeight.Value;
            }

            return total;
        }

        /// <summary>
        /// Walks the working candidates in canonical order, returning the index whose cumulative weight
        /// range contains the draw. Zero-weight candidates contribute nothing and are skipped.
        /// </summary>
        /// <param name="candidates">The working candidates.</param>
        /// <param name="draw">The draw in the range [0, total weight).</param>
        /// <returns>The selected candidate index.</returns>
        private static int WalkToWeighted(List<CandidateEntry> candidates, uint draw)
        {
            ulong cumulative = 0UL;
            for (int index = 0; index < candidates.Count; index++)
            {
                if (candidates[index].FinalWeight.HasValue)
                    cumulative += (ulong)candidates[index].FinalWeight.Value;
                if (draw < cumulative)
                    return index;
            }

            return candidates.Count - 1;
        }
    }
}