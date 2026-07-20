using System;
using System.Collections.Generic;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// The captured, canonically-ordered, immutable candidate set. Construction sorts entries into the
    /// single CAB-EVT-799 order via <see cref="CandidateOrderingKey.Compare"/> and validates identity
    /// uniqueness (CAB-EVT-798), the absence of an ordering collision between distinct entries
    /// (CAB-EVT-806), and that entries are all weighted or all unweighted. Empty snapshots are legal —
    /// insufficiency handles them.
    /// </summary>
    public sealed class CandidateSnapshot
    {
        /// <summary>
        /// The candidates in canonical order.
        /// </summary>
        public IReadOnlyList<CandidateEntry> Candidates { get; }

        /// <summary>
        /// The number of candidates.
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Whether every candidate carries a weight; false for an empty snapshot.
        /// </summary>
        public bool IsWeighted { get; }

        private CandidateSnapshot(IReadOnlyList<CandidateEntry> candidates, bool isWeighted)
        {
            Candidates = candidates;
            Count = candidates.Count;
            IsWeighted = isWeighted;
        }

        /// <summary>
        /// Creates a snapshot from <paramref name="entries"/>: copies, sorts into canonical order, and
        /// validates uniqueness, ordering distinctness, and uniform weight presence.
        /// </summary>
        /// <param name="entries">The candidate entries in any order.</param>
        /// <returns>The immutable, canonically-ordered snapshot.</returns>
        public static CandidateSnapshot Create(IReadOnlyList<CandidateEntry> entries)
        {
            if (entries == null)
                throw new ArgumentException("A candidate snapshot requires an entry list.", nameof(entries));

            List<CandidateEntry> working = new List<CandidateEntry>(entries.Count);
            HashSet<string> identities = new HashSet<string>(StringComparer.Ordinal);
            int weightedCount = 0;
            for (int index = 0; index < entries.Count; index++)
            {
                CandidateEntry entry = entries[index];
                if (entry == null)
                {
                    throw new ArgumentException(
                        "A candidate entry must not be null.", 
                        nameof(entries)
                    );
                }
                if (!identities.Add(entry.Identity))
                {
                    throw new ArgumentException(
                        "Candidate identities must be unique within a snapshot.",
                        nameof(entries)
                    );
                }
                if (entry.FinalWeight.HasValue)
                    weightedCount += 1;

                working.Add(entry);
            }

            if (weightedCount != 0 && weightedCount != working.Count)
                throw new ArgumentException(
                    "A snapshot's entries must be all weighted or all unweighted.",
                    nameof(entries));

            SortByCanonicalOrder(working);

            for (int index = 1; index < working.Count; index++)
            {
                if (CandidateOrderingKey.Compare(working[index - 1].OrderingKey, working[index].OrderingKey) == 0)
                {
                    throw new ArgumentException(
                        "Distinct candidates must have distinct ordering keys.",
                        nameof(entries)
                    );
                }
            }

            bool isWeighted = working.Count > 0 && weightedCount == working.Count;
            return new CandidateSnapshot(working, isWeighted);
        }

        /// <summary>
        /// Sorts the entries in place into canonical order by insertion sort (no LINQ; small boundary sets).
        /// </summary>
        /// <param name="entries">The entries to sort in place.</param>
        private static void SortByCanonicalOrder(List<CandidateEntry> entries)
        {
            for (int position = 1; position < entries.Count; position++)
            {
                CandidateEntry current = entries[position];
                int scan = position - 1;
                while (scan >= 0 && CandidateOrderingKey.Compare(entries[scan].OrderingKey, current.OrderingKey) > 0)
                {
                    entries[scan + 1] = entries[scan];
                    scan -= 1;
                }

                entries[scan + 1] = current;
            }
        }
    }
}