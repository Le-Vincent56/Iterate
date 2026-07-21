using System;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// One entry in the missing-evidence ledger: the field that was required but not recorded and why,
    /// anchored to at most one record. An event identity anchors an event-scoped defect, a unit identity
    /// a unit-scoped defect, and both null an execution-scoped defect; a defect may not anchor to both an
    /// event and a unit at once.
    /// </summary>
    public sealed record EvidenceDefect
    {
        /// <summary>
        /// The event this defect is anchored to, or null when not event-scoped.
        /// </summary>
        public TraceEventID? Event { get; }

        /// <summary>
        /// The unit this defect is anchored to, or null when not unit-scoped.
        /// </summary>
        public RuntimeUnitID? Unit { get; }

        /// <summary>
        /// The name of the required-but-missing field; never empty.
        /// </summary>
        public string FieldName { get; }

        /// <summary>
        /// The reason the evidence is defective; never empty.
        /// </summary>
        public string Reason { get; }

        public EvidenceDefect(
            TraceEventID? eventIdentity,
            RuntimeUnitID? unit,
            string fieldName,
            string reason
        )
        {
            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentException("A defect requires a field name.", nameof(fieldName));
            
            if (string.IsNullOrEmpty(reason))
                throw new ArgumentException("A defect requires a reason.", nameof(reason));
            
            if (eventIdentity != null && unit != null)
                throw new ArgumentException("A defect anchors to at most one record; supply an event or a unit, not both.", nameof(unit));

            Event = eventIdentity;
            Unit = unit;
            FieldName = fieldName;
            Reason = reason;
        }
    }
}