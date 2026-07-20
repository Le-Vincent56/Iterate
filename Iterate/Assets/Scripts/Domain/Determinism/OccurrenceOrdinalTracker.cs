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
    }
}