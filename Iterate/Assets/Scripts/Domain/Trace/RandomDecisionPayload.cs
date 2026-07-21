using System;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The random-decision evidence: the shipped decision record preserved whole. The record already
    /// compares structurally, so synthesized equality carries the replay-through-storage property — a
    /// recomputed decision equals the stored one.
    /// </summary>
    public sealed record RandomDecisionPayload : EventPayload
    {
        /// <summary>
        /// The complete decision record; never null.
        /// </summary>
        public RandomDecisionRecord Record { get; }

        public RandomDecisionPayload(RandomDecisionRecord record)
        {
            Record = record ?? throw new ArgumentException("A random-decision payload requires a record.", nameof(record));
        }
    }
}