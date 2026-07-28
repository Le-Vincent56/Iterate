using System;
using System.Collections.Generic;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Diagnostics
{
    /// <summary>
    /// Derives the effect-resolution views of one execution record: what each effect origin did across
    /// the execution, what happened at each boundary offer, and how events caused and contained one
    /// another. Every derivation is a pure read of the frozen record — nothing here invokes the engine,
    /// constructs evidence, or mutates anything.
    /// This is the whole answer to "why did this effect fire, or not". The engine already records it:
    /// a near-miss is an EFFECT_FAILED_TO_QUALIFY event caused by the candidate it observed, carrying
    /// the failed requirement in its disposition reason. What was missing was a typed reading of it.
    /// The honest limit, which the views cannot close: structural ineligibility and consumed allowances
    /// emit no evidence, so this projection can say "it near-missed, on this requirement" but never "it
    /// was never a candidate". An absent entry means the record is silent about that effect at that
    /// boundary — which is either of those two things, and the projection does not guess which.
    /// </summary>
    public static class EffectResolutionProjection
    {
        /// <summary>
        /// Groups every qualification-family event by its effect origin, chronologically.
        /// </summary>
        /// <param name="record">The frozen record to read.</param>
        /// <returns>One timeline per effect origin that resolved at least once, in first-seen order.</returns>
        public static IReadOnlyList<EffectResolutionTimeline> TimelinesOf(ExecutionRecord record)
        {
            if (record == null)
                throw new ArgumentException("A projection requires a record.", nameof(record));

            List<InstanceID> origins = new List<InstanceID>();
            List<InstanceID?> hosts = new List<InstanceID?>();
            List<List<EffectResolutionEntry>> grouped = new List<List<EffectResolutionEntry>>();

            for (int index = 0; index < record.Events.Count; index++)
            {
                EventRecord entry = record.Events[index];

                if (!TryReadEntry(entry, out EffectResolutionEntry resolution))
                    continue;

                InstanceID origin = entry.Evidence.EffectOriginInstance.Value;
                int slot = origins.IndexOf(origin);

                if (slot < 0)
                {
                    origins.Add(origin);
                    hosts.Add(entry.Evidence.HostInstance);
                    grouped.Add(new List<EffectResolutionEntry>());
                    slot = origins.Count - 1;
                }
                else if (hosts[slot] == null)
                {
                    hosts[slot] = entry.Evidence.HostInstance;
                }

                grouped[slot].Add(resolution);
            }

            List<EffectResolutionTimeline> timelines = new List<EffectResolutionTimeline>(origins.Count);
            for (int index = 0; index < origins.Count; index++)
            {
                timelines.Add(new EffectResolutionTimeline(origins[index], hosts[index], grouped[index]));
            }

            return timelines;
        }

        /// <summary>
        /// Groups every qualification-family event by the candidate event it observed. Entries recorded
        /// with no candidate belong to no offer and are omitted here; they remain on their timeline.
        /// </summary>
        /// <param name="record">The frozen record to read.</param>
        /// <returns>One view per candidate event that drew at least one resolution, in first-seen order.</returns>
        public static IReadOnlyList<BoundaryOfferView> OffersOf(ExecutionRecord record)
        {
            if (record == null)
                throw new ArgumentException("A projection requires a record.", nameof(record));

            List<TraceEventID> candidates = new List<TraceEventID>();
            List<List<EffectResolutionEntry>> grouped = new List<List<EffectResolutionEntry>>();

            for (int index = 0; index < record.Events.Count; index++)
            {
                EventRecord entry = record.Events[index];

                if (!TryReadEntry(entry, out EffectResolutionEntry resolution))
                    continue;

                if (resolution.CausingEvent == null)
                    continue;

                TraceEventID candidate = resolution.CausingEvent.Value;
                int slot = candidates.IndexOf(candidate);

                if (slot < 0)
                {
                    candidates.Add(candidate);
                    grouped.Add(new List<EffectResolutionEntry>());
                    slot = candidates.Count - 1;
                }

                grouped[slot].Add(resolution);
            }

            List<BoundaryOfferView> offers = new List<BoundaryOfferView>(candidates.Count);
            for (int index = 0; index < candidates.Count; index++)
            {
                offers.Add(new BoundaryOfferView(candidates[index], grouped[index]));
            }

            return offers;
        }

        /// <summary>
        /// Builds the causal and containment index over the record.
        /// </summary>
        /// <param name="record">The frozen record to read.</param>
        /// <returns>The causality index.</returns>
        public static TraceCausality CausalityOf(ExecutionRecord record)
        {
            return new TraceCausality(record);
        }

        /// <summary>
        /// Reads one event as a resolution entry when it is a qualification-family event carrying one of
        /// the three effect subtypes and an effect origin.
        /// </summary>
        /// <param name="record">The event record.</param>
        /// <param name="entry">The resolution entry when the event is one.</param>
        /// <returns>True when the event is a resolution entry.</returns>
        private static bool TryReadEntry(EventRecord record, out EffectResolutionEntry entry)
        {
            entry = null;
            EventEvidence evidence = record.Evidence;

            if (evidence.Family != EventFamilies.Qualification)
                return false;

            if (evidence.EffectOriginInstance == null)
                return false;

            EffectResolutionEntryKind kind;

            switch (evidence.Subtype)
            {
                case ExecutionEventSubtypes.EffectQualified:
                    kind = EffectResolutionEntryKind.Qualified;
                    break;

                case ExecutionEventSubtypes.EffectCommitted:
                    kind = EffectResolutionEntryKind.Committed;
                    break;

                case ExecutionEventSubtypes.EffectFailedToQualify:
                    kind = EffectResolutionEntryKind.FailedToQualify;
                    break;

                default:
                    return false;
            }

            entry = new EffectResolutionEntry(
                record.Identity,
                kind,
                kind == EffectResolutionEntryKind.FailedToQualify ? evidence.DispositionReason : null,
                evidence.CausingEvent,
                evidence.ContainingUnit,
                evidence.CausalDepth);

            return true;
        }
    }
}