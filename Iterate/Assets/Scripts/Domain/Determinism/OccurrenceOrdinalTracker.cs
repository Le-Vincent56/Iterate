using System;
using System.Collections.Generic;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// A small, caller-owned tracker of per-purpose, per-scope occurrence ordinals. The
    /// first <see cref="Next"/> for a (selection purpose, governing context scope identity) pair returns 1,
    /// then 2, 3, and so on; distinct pairs advance independently. The Determinism Service never sees or
    /// advances a tracker — the ordinal reaches it only as a context component — so prediction and replay
    /// stay correct by construction. Ordinal string semantics: the pair key compares ordinally and
    /// case-sensitively.
    /// Lifetime contract: hold one tracker per owning scope and discard it with that scope. The scope
    /// identity must make unrelated same-purpose decisions distinct; only intentionally ordered sequences
    /// share a key. A tracker that outlives its scope — or is shared across scopes that should
    /// be independent — produces valid-but-different contexts that no diagnostic catches.
    /// </summary>
    public sealed class OccurrenceOrdinalTracker
    {
        /// <summary>
        /// The per-(purpose, scope) counters; the value is the highest ordinal issued so far for the pair.
        /// </summary>
        private readonly Dictionary<(string, string), int> _counters = new();

        /// <summary>
        /// Returns the next one-based occurrence ordinal for the (purpose, scope) pair.
        /// </summary>
        /// <param name="selectionPurpose">The selection purpose; non-empty.</param>
        /// <param name="governingContextIdentity">The governing context scope identity; non-empty.</param>
        /// <returns>1 for the first call on a pair, then 2, 3, and so on.</returns>
        public int Next(string selectionPurpose, string governingContextIdentity)
        {
            if (string.IsNullOrEmpty(selectionPurpose))
                throw new ArgumentException("A selection purpose is required.", nameof(selectionPurpose));

            if (string.IsNullOrEmpty(governingContextIdentity))
            {
                throw new ArgumentException(
                    "A governing context identity is required.",
                    nameof(governingContextIdentity)
                );
            }

            (string, string) key = (selectionPurpose, governingContextIdentity);
            int next = _counters.TryGetValue(key, out int current) ? current + 1 : 1;
            _counters[key] = next;
            return next;
        }
        
        /// <summary>
        /// Takes the pairs advanced so far and their highest issued ordinals, ordered by selection
        /// purpose then governing context, both ordinally. The order is fixed rather than the
        /// dictionary's, so two Sessions in the same state always write the same bytes.
        /// </summary>
        /// <returns>The counters in a stable order; empty when nothing has been advanced.</returns>
        public IReadOnlyList<OrdinalCounter> Snapshot()
        {
            List<OrdinalCounter> counters = new(_counters.Count);
            foreach (KeyValuePair<(string, string), int> counter in _counters)
            {
                counters.Add(new OrdinalCounter(counter.Key.Item1, counter.Key.Item2, counter.Value));
            }

            counters.Sort(CompareCounters);
            return counters;
        }

        /// <summary>
        /// Rebuilds a tracker that has already issued the given ordinals. Each listed pair continues
        /// from its count; a pair the list does not name still starts at one.
        /// </summary>
        /// <param name="counters">The counters to continue from.</param>
        /// <returns>The restored tracker.</returns>
        /// <exception cref="ArgumentException">Thrown on a null list or counter, an empty purpose or context, a count below one, or a repeated pair.</exception>
        public static OccurrenceOrdinalTracker Restore(IReadOnlyList<OrdinalCounter> counters)
        {
            if (counters == null)
                throw new ArgumentException("A restored tracker requires a counter list.", nameof(counters));

            OccurrenceOrdinalTracker tracker = new();
            for (int index = 0; index < counters.Count; index++)
            {
                OrdinalCounter counter = counters[index];
                if (counter == null)
                    throw new ArgumentException("A restored counter is required.", nameof(counters));

                if (string.IsNullOrEmpty(counter.SelectionPurpose))
                    throw new ArgumentException("A selection purpose is required.", nameof(counters));

                if (string.IsNullOrEmpty(counter.GoverningContextIdentity))
                    throw new ArgumentException("A governing context identity is required.", nameof(counters));

                if (counter.Count < 1)
                    throw new ArgumentException("A restored occurrence ordinal is at least one.", nameof(counters));

                (string, string) key = (counter.SelectionPurpose, counter.GoverningContextIdentity);
                if (tracker._counters.ContainsKey(key))
                    throw new ArgumentException("A restored tracker cannot repeat a pair.", nameof(counters));

                tracker._counters[key] = counter.Count;
            }

            return tracker;
        }

        /// <summary>
        /// Orders two counters by selection purpose then governing context, both ordinally.
        /// </summary>
        /// <param name="left">The first counter.</param>
        /// <param name="right">The second counter.</param>
        /// <returns>A negative value, zero or a positive value as the first sorts before, with or after the second.</returns>
        private static int CompareCounters(OrdinalCounter left, OrdinalCounter right)
        {
            int byPurpose = string.CompareOrdinal(left.SelectionPurpose, right.SelectionPurpose);
            if (byPurpose != 0)
                return byPurpose;

            return string.CompareOrdinal(left.GoverningContextIdentity, right.GoverningContextIdentity);
        }
    }
}