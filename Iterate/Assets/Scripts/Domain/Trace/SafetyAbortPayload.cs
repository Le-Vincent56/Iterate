using System;
using System.Collections.Generic;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The evidence of a safety abort: the identity of the occurrence that would have exceeded a limit
    /// and was never created, the affected unit, and every simultaneously breached limit in declared
    /// order. The breached-limit list is never empty and carries no hidden priority — all listed limits
    /// were violated together. Equality is structural over the list.
    /// </summary>
    public sealed record SafetyAbortPayload : EventPayload
    {
        /// <summary>
        /// The identity of the over-limit occurrence that was never created; never empty.
        /// </summary>
        public string OverLimitOccurrenceIdentity { get; }

        /// <summary>
        /// The unit whose activity breached the limit.
        /// </summary>
        public RuntimeUnitID AffectedUnit { get; }

        /// <summary>
        /// The simultaneously breached limits in declared order; never null, never empty.
        /// </summary>
        public IReadOnlyList<BreachedLimit> BreachedLimits { get; }

        public SafetyAbortPayload(
            string overLimitOccurrenceIdentity,
            RuntimeUnitID affectedUnit,
            IReadOnlyList<BreachedLimit> breachedLimits
        )
        {
            if (string.IsNullOrEmpty(overLimitOccurrenceIdentity))
                throw new ArgumentException("A safety-abort payload requires an occurrence identity.", nameof(overLimitOccurrenceIdentity));
            
            if (breachedLimits == null || breachedLimits.Count == 0)
                throw new ArgumentException("A safety-abort payload requires at least one breached limit.", nameof(breachedLimits));

            OverLimitOccurrenceIdentity = overLimitOccurrenceIdentity;
            AffectedUnit = affectedUnit;
            BreachedLimits = breachedLimits;
        }

        /// <summary>
        /// Structural value equality: the occurrence identity and affected unit plus element-wise limit
        /// comparison.
        /// </summary>
        /// <param name="other">The payload to compare against.</param>
        /// <returns>True when the identity, unit, and limits are equal.</returns>
        public bool Equals(SafetyAbortPayload other)
        {
            return base.Equals(other)
                && string.Equals(OverLimitOccurrenceIdentity, other?.OverLimitOccurrenceIdentity, StringComparison.Ordinal)
                && AffectedUnit == other?.AffectedUnit
                && TraceEquality.ListEquals(BreachedLimits, other.BreachedLimits);
        }

        /// <summary>
        /// A hash consistent with structural equality, over the occurrence identity, affected unit, and
        /// limit count.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + OverLimitOccurrenceIdentity.GetHashCode();
                hash = hash * 31 + AffectedUnit.GetHashCode();
                hash = hash * 31 + BreachedLimits.Count;
                return hash;
            }
        }
    }
}