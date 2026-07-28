using System;
using System.Collections.Generic;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Diagnostics
{
    /// <summary>
    /// One candidate event and everything that happened because of it: who qualified, who committed,
    /// and who near-missed on which requirement. This is the per-offer view of the same evidence the
    /// timelines group per origin — the answer to "who saw this event, and what did they do about it".
    /// Entries the engine recorded with no candidate event belong to no offer and appear only on their
    /// origin's timeline.
    /// </summary>
    public sealed record BoundaryOfferView
    {
        /// <summary>
        /// The candidate event the effects observed.
        /// </summary>
        public TraceEventID CandidateEvent { get; }

        /// <summary>
        /// The resolution entries this candidate caused, in chronological order; never null, never empty.
        /// </summary>
        public IReadOnlyList<EffectResolutionEntry> Entries { get; }

        public BoundaryOfferView(TraceEventID candidateEvent, IReadOnlyList<EffectResolutionEntry> entries)
        {
            if (entries == null)
                throw new ArgumentException("An offer view requires an entries list.", nameof(entries));

            if (entries.Count == 0)
                throw new ArgumentException("An offer view with no entries is not projected at all.", nameof(entries));

            CandidateEvent = candidateEvent;
            Entries = entries;
        }
    }
}