using System;
using System.Collections.Generic;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// The complete return value of <c>DeterminismService.Decide</c>: the outcome, the full decision
    /// record, and the ordered event payloads. Equality is structural over the events and nested records.
    /// </summary>
    public sealed record DecisionResult
    {
        /// <summary>
        /// The resolved outcome.
        /// </summary>
        public DecisionOutcome Outcome { get; }

        /// <summary>
        /// The complete decision record.
        /// </summary>
        public RandomDecisionRecord Record { get; }

        /// <summary>
        /// The ordered event payloads emitted across the decision's lifecycle.
        /// </summary>
        public IReadOnlyList<RandomSelectionEvent> Events { get; }

        public DecisionResult(
            DecisionOutcome outcome,
            RandomDecisionRecord record,
            IReadOnlyList<RandomSelectionEvent> events
        )
        {
            Outcome = outcome ?? throw new ArgumentException("A decision result requires an outcome.", nameof(outcome));
            Record = record ?? throw new ArgumentException("A decision result requires a record.", nameof(record));
            Events = events ?? throw new ArgumentException("A decision result requires an events list.", nameof(events));
        }

        /// <summary>
        /// Structural value equality over the outcome, record, and element-wise events.
        /// </summary>
        /// <param name="other">The result to compare against.</param>
        /// <returns>True when the outcome, record, and events are equal.</returns>
        public bool Equals(DecisionResult other)
        {
            if (other == null) return false;

            return Outcome == other.Outcome
                && Record == other.Record
                && DeterminismEquality.ListEquals(Events, other.Events
            );
        }

        /// <summary>
        /// A hash consistent with structural equality, over the outcome, record, and event count.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Outcome.GetHashCode();
                hash = hash * 31 + Record.GetHashCode();
                hash = hash * 31 + Events.Count;
                return hash;
            }
        }
    }
}