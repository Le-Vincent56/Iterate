using System;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// One event record: its index-stable identity paired with its validated evidence. The chronological
    /// order is the identity ordinal, since append order is canonical chronological order. This is a value
    /// type carrying one identity and one evidence reference; its equality delegates to the evidence's
    /// structural equality, so two records with equal identities and independently built equal evidence
    /// compare equal.
    /// </summary>
    public readonly record struct EventRecord
    {
        /// <summary>
        /// The index-stable identity of this event.
        /// </summary>
        public TraceEventID Identity { get; }

        /// <summary>
        /// The validated evidence of this event; never null.
        /// </summary>
        public EventEvidence Evidence { get; }

        /// <summary>
        /// The canonical chronological order, equal to the identity ordinal.
        /// </summary>
        public int ChronologicalOrder => Identity.Value;

        public EventRecord(TraceEventID identity, EventEvidence evidence)
        {
            Identity = identity;
            Evidence = evidence ?? throw new ArgumentException("An event record requires evidence.", nameof(evidence));
        }
    }
}