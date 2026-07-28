using System;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Diagnostics
{
    /// <summary>
    /// One thing one effect origin did at one boundary, read from a single qualification-family event:
    /// the event itself, what kind of resolution it records, the requirement that failed when it is a
    /// near-miss, the candidate event it observed, the unit it happened inside, and its causal depth.
    /// The failed requirement is the engine's own rendering of the requirement in kind:value form,
    /// passed through verbatim rather than re-worded, so the debugger and the trace agree exactly.
    /// </summary>
    public sealed record EffectResolutionEntry
    {
        /// <summary>
        /// The qualification event this entry reads.
        /// </summary>
        public TraceEventID Event { get; }

        /// <summary>
        /// What the effect did at this boundary.
        /// </summary>
        public EffectResolutionEntryKind Kind { get; }

        /// <summary>
        /// The requirement that failed, non-null exactly when the kind is
        /// <see cref="EffectResolutionEntryKind.FailedToQualify"/>.
        /// </summary>
        public string FailedRequirement { get; }

        /// <summary>
        /// The candidate event the effect observed, or null when the offer carried no candidate.
        /// </summary>
        public TraceEventID? CausingEvent { get; }

        /// <summary>
        /// The unit this resolution happened inside, or null when the offer was unit-less.
        /// </summary>
        public RuntimeUnitID? ContainingUnit { get; }

        /// <summary>
        /// The event's causal depth.
        /// </summary>
        public int CausalDepth { get; }

        public EffectResolutionEntry(
            TraceEventID resolutionEvent,
            EffectResolutionEntryKind kind,
            string failedRequirement,
            TraceEventID? causingEvent,
            RuntimeUnitID? containingUnit,
            int causalDepth
        )
        {
            if (kind == EffectResolutionEntryKind.FailedToQualify && string.IsNullOrEmpty(failedRequirement))
                throw new ArgumentException("A near-miss entry requires the requirement that failed.", nameof(failedRequirement));

            if (kind != EffectResolutionEntryKind.FailedToQualify && failedRequirement != null)
                throw new ArgumentException("Only a near-miss entry carries a failed requirement.", nameof(failedRequirement));

            if (causalDepth < 0)
                throw new ArgumentException("Causal depth may not be negative.", nameof(causalDepth));

            Event = resolutionEvent;
            Kind = kind;
            FailedRequirement = failedRequirement;
            CausingEvent = causingEvent;
            ContainingUnit = containingUnit;
            CausalDepth = causalDepth;
        }
    }
}