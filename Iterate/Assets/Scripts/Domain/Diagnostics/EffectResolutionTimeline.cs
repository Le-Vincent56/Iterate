using System;
using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Diagnostics
{
    /// <summary>
    /// Everything one effect origin did across one execution, in chronological order: the origin
    /// instance, the host it is socketed into when it has one, and its resolution entries.
    /// A timeline records only what the trace carries. Structural ineligibility and consumed allowances
    /// emit no evidence at all, so an effect that was never a candidate produces no timeline and an
    /// effect whose allowance was already spent produces no entry for that boundary — the two are
    /// indistinguishable here, and neither is guessed at. The absence of an entry means the record is
    /// silent, never that the effect was ineligible.
    /// </summary>
    public sealed record EffectResolutionTimeline
    {
        /// <summary>
        /// The effect-origin instance this timeline belongs to.
        /// </summary>
        public InstanceID Origin { get; }

        /// <summary>
        /// The host instance the origin is socketed into, or null when it has none.
        /// </summary>
        public InstanceID? Host { get; }

        /// <summary>
        /// The origin's resolution entries in chronological order; never null, never empty.
        /// </summary>
        public IReadOnlyList<EffectResolutionEntry> Entries { get; }

        public EffectResolutionTimeline(InstanceID origin, InstanceID? host, IReadOnlyList<EffectResolutionEntry> entries)
        {
            if (entries == null)
                throw new ArgumentException("A timeline requires an entries list.", nameof(entries));

            if (entries.Count == 0)
                throw new ArgumentException("A timeline with no entries is not projected at all.", nameof(entries));

            Origin = origin;
            Host = host;
            Entries = entries;
        }
    }
}