using System;
using System.Collections.Generic;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The frozen evidence of one execution: its reproduction header, its unit and event records, the
    /// derived traversal, threshold, and counter histories, the safety tallies and status, the final
    /// register state, and the missing-evidence ledger. Three of the four statuses are derived at
    /// construction so their separation holds by construction: completeness from whether the ledger is
    /// empty, validity from completion-and-completeness, and handoff eligibility from validity. The
    /// completion status is supplied and cross-checked against the safety status. Equality is structural
    /// over the six lists.
    /// </summary>
    public sealed record ExecutionRecord
    {
        /// <summary>
        /// The reproduction header; never null.
        /// </summary>
        public ExecutionEvidenceHeader Header { get; }

        /// <summary>
        /// The unit records in identity order; never null.
        /// </summary>
        public IReadOnlyList<RuntimeUnitRecord> Units { get; }

        /// <summary>
        /// The event records in chronological order; never null.
        /// </summary>
        public IReadOnlyList<EventRecord> Events { get; }

        /// <summary>
        /// The canonical-traversal units in start order; never null.
        /// </summary>
        public IReadOnlyList<RuntimeUnitID> TraversalOrder { get; }

        /// <summary>
        /// The threshold-family events in order; never null.
        /// </summary>
        public IReadOnlyList<TraceEventID> ThresholdHistory { get; }

        /// <summary>
        /// The Process-counter quantity events in order; never null.
        /// </summary>
        public IReadOnlyList<TraceEventID> CounterHistory { get; }

        /// <summary>
        /// The high-water safety tallies.
        /// </summary>
        public SafetyCounts SafetyCounts { get; }

        /// <summary>
        /// The safety status of the execution.
        /// </summary>
        public SafetyStatus SafetyStatus { get; }

        /// <summary>
        /// The final register state; never null.
        /// </summary>
        public FinalExecutionState FinalState { get; }

        /// <summary>
        /// The missing-evidence ledger; never null, empty when the trace is complete.
        /// </summary>
        public IReadOnlyList<EvidenceDefect> Defects { get; }

        /// <summary>
        /// Whether the execution completed or was aborted.
        /// </summary>
        public ExecutionCompletionStatus CompletionStatus { get; }

        /// <summary>
        /// The derived trace completeness: complete when the ledger is empty, incomplete otherwise.
        /// </summary>
        public TraceCompletenessStatus TraceCompleteness { get; }

        /// <summary>
        /// The derived result validity: valid only when the execution completed and the trace is complete.
        /// </summary>
        public ResultValidityStatus ResultValidity { get; }

        /// <summary>
        /// The derived handoff status: eligible when the result is valid, blocked otherwise.
        /// </summary>
        public ResultReviewHandoffStatus HandoffStatus { get; }

        public ExecutionRecord(
            ExecutionEvidenceHeader header,
            IReadOnlyList<RuntimeUnitRecord> units,
            IReadOnlyList<EventRecord> events,
            IReadOnlyList<RuntimeUnitID> traversalOrder,
            IReadOnlyList<TraceEventID> thresholdHistory,
            IReadOnlyList<TraceEventID> counterHistory,
            SafetyCounts safetyCounts,
            SafetyStatus safetyStatus,
            FinalExecutionState finalState,
            IReadOnlyList<EvidenceDefect> defects,
            ExecutionCompletionStatus completionStatus
        )
        {
            switch (completionStatus)
            {
                case ExecutionCompletionStatus.SafetyAborted when safetyStatus != SafetyStatus.SafetyAborted:
                    throw new ArgumentException("An aborted completion requires an aborted safety status.", nameof(safetyStatus));
                
                case ExecutionCompletionStatus.Completed when safetyStatus == SafetyStatus.SafetyAborted:
                    throw new ArgumentException("A completed execution may not carry an aborted safety status.", nameof(safetyStatus));
            }

            Header = header ?? throw new ArgumentException("An execution record requires a header.", nameof(header));
            Units = units ?? throw new ArgumentException("An execution record requires a units list.", nameof(units));
            Events = events ?? throw new ArgumentException("An execution record requires an events list.", nameof(events));
            TraversalOrder = traversalOrder ?? throw new ArgumentException("An execution record requires a traversal-order list.", nameof(traversalOrder));
            ThresholdHistory = thresholdHistory ?? throw new ArgumentException("An execution record requires a threshold-history list.", nameof(thresholdHistory));
            CounterHistory = counterHistory ?? throw new ArgumentException("An execution record requires a counter-history list.", nameof(counterHistory));
            SafetyCounts = safetyCounts;
            SafetyStatus = safetyStatus;
            FinalState = finalState ?? throw new ArgumentException("An execution record requires a final state.", nameof(finalState));
            Defects = defects ?? throw new ArgumentException("An execution record requires a defects list.", nameof(defects));
            CompletionStatus = completionStatus;

            TraceCompleteness = defects.Count == 0
                ? TraceCompletenessStatus.Complete
                : TraceCompletenessStatus.Incomplete;
            ResultValidity = completionStatus == ExecutionCompletionStatus.Completed && TraceCompleteness == TraceCompletenessStatus.Complete
                ? ResultValidityStatus.Valid
                : ResultValidityStatus.Invalid;
            HandoffStatus = ResultValidity == ResultValidityStatus.Valid
                ? ResultReviewHandoffStatus.Eligible
                : ResultReviewHandoffStatus.Blocked;
        }

        /// <summary>
        /// Structural value equality: the header, counts, statuses, and final state plus element-wise
        /// comparison of the six lists.
        /// </summary>
        /// <param name="other">The record to compare against.</param>
        /// <returns>True when every field and every list are equal.</returns>
        public bool Equals(ExecutionRecord other)
        {
            if (other == null) return false;

            return Header == other.Header
                && SafetyCounts == other.SafetyCounts
                && SafetyStatus == other.SafetyStatus
                && FinalState == other.FinalState
                && CompletionStatus == other.CompletionStatus
                && TraceCompleteness == other.TraceCompleteness
                && ResultValidity == other.ResultValidity
                && HandoffStatus == other.HandoffStatus
                && TraceEquality.ListEquals(Units, other.Units)
                && TraceEquality.ListEquals(Events, other.Events)
                && TraceEquality.ListEquals(TraversalOrder, other.TraversalOrder)
                && TraceEquality.ListEquals(ThresholdHistory, other.ThresholdHistory)
                && TraceEquality.ListEquals(CounterHistory, other.CounterHistory)
                && TraceEquality.ListEquals(Defects, other.Defects
           );
        }

        /// <summary>
        /// A hash consistent with structural equality, over the completion status, derived validity, and
        /// the unit, event, and defect counts.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)CompletionStatus;
                hash = hash * 31 + (int)ResultValidity;
                hash = hash * 31 + Units.Count;
                hash = hash * 31 + Events.Count;
                hash = hash * 31 + Defects.Count;
                return hash;
            }
        }
    }
}