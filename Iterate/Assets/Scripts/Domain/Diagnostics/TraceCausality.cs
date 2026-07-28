using System;
using System.Collections.Generic;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Diagnostics
{
    /// <summary>
    /// A read-only index over one record's two event linkages, built once at construction in a single
    /// pass. Causation is the causing-event chain — what an event happened because of — and containment
    /// is the parent-event chain, which is what an event happened inside. They are indexed separately
    /// because they are separate: an event may have either, both, or neither.
    /// Every query is total. An identity the record does not hold, and an event with no cause or no
    /// children, all answer empty rather than throwing, because selecting a root candidate is an
    /// ordinary thing for a viewer to do. The ancestor walk is bounded by the event count: nothing
    /// validates that the causing chain is acyclic — not the event constructor, which sees one event at
    /// a time, nor the record constructor, which never inspects the links — so a defectively-stored
    /// record could cycle, and a diagnostic asked to explain a corrupt record must answer rather than
    /// hang.
    /// </summary>
    public sealed class TraceCausality
    {
        private readonly Dictionary<TraceEventID, TraceEventID> _causes;
        private readonly Dictionary<TraceEventID, List<TraceEventID>> _consequences;
        private readonly Dictionary<TraceEventID, List<TraceEventID>> _children;
        private readonly int _eventCount;

        public TraceCausality(ExecutionRecord record)
        {
            if (record == null)
                throw new ArgumentException("A causality index requires a record.", nameof(record));

            _causes = new Dictionary<TraceEventID, TraceEventID>(record.Events.Count);
            _consequences = new Dictionary<TraceEventID, List<TraceEventID>>(record.Events.Count);
            _children = new Dictionary<TraceEventID, List<TraceEventID>>(record.Events.Count);
            _eventCount = record.Events.Count;

            for (int index = 0; index < record.Events.Count; index++)
            {
                EventRecord entry = record.Events[index];
                TraceEventID? causing = entry.Evidence.CausingEvent;
                TraceEventID? parent = entry.Evidence.ParentEvent;

                if (causing != null)
                {
                    _causes[entry.Identity] = causing.Value;
                    Append(_consequences, causing.Value, entry.Identity);
                }

                if (parent != null)
                    Append(_children, parent.Value, entry.Identity);
            }
        }

        /// <summary>
        /// The causing-event chain from the given event to its root, nearest cause first.
        /// </summary>
        /// <param name="identity">The event to walk from.</param>
        /// <returns>The ancestors, empty when the event is uncaused or unknown.</returns>
        public IReadOnlyList<TraceEventID> AncestorsOf(TraceEventID identity)
        {
            List<TraceEventID> ancestors = new List<TraceEventID>();
            TraceEventID current = identity;

            while (ancestors.Count < _eventCount && _causes.TryGetValue(current, out TraceEventID cause))
            {
                ancestors.Add(cause);
                current = cause;
            }

            return ancestors;
        }

        /// <summary>
        /// Every event directly caused by the given event, in record order. Direct only, never
        /// transitive.
        /// </summary>
        /// <param name="identity">The causing event.</param>
        /// <returns>The direct consequences, empty when there are none.</returns>
        public IReadOnlyList<TraceEventID> ConsequencesOf(TraceEventID identity)
        {
            return _consequences.TryGetValue(identity, out List<TraceEventID> found)
                ? found
                : Array.Empty<TraceEventID>();
        }

        /// <summary>
        /// Every event naming the given event as its containment parent, in record order.
        /// </summary>
        /// <param name="identity">The parent event.</param>
        /// <returns>The containment children, empty when there are none.</returns>
        public IReadOnlyList<TraceEventID> ContainmentChildrenOf(TraceEventID identity)
        {
            return _children.TryGetValue(identity, out List<TraceEventID> found)
                ? found
                : Array.Empty<TraceEventID>();
        }

        /// <summary>
        /// Appends one identity to the list stored under the key, creating the list on first use.
        /// </summary>
        /// <param name="index">The index to append into.</param>
        /// <param name="key">The key.</param>
        /// <param name="value">The identity to append.</param>
        private static void Append(Dictionary<TraceEventID, List<TraceEventID>> index, TraceEventID key, TraceEventID value)
        {
            if (!index.TryGetValue(key, out List<TraceEventID> found))
            {
                found = new List<TraceEventID>();
                index[key] = found;
            }

            found.Add(value);
        }
    }
}