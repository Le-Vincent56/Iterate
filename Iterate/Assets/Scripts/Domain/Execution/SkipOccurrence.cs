using System;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One skipped source-execution attempt offered to the effect engine at the pre-operation band:
    /// the skipped unit, its skip event, the causal depth, ownership with the owning host instance,
    /// the explicit skip cause, and whether the skip is structurally rescuable.
    /// </summary>
    /// <param name="Unit">The skipped runtime unit.</param>
    /// <param name="SkipEvent">The skip event this occurrence mirrors.</param>
    /// <param name="CausalDepth">The skip event's causal depth; never negative.</param>
    /// <param name="Ownership">The skipped unit's ownership classification.</param>
    /// <param name="HostInstance">The owning host instance; non-null exactly when player-owned.</param>
    /// <param name="SkipCause">The explicit skip cause; never empty.</param>
    /// <param name="Rescuable">Whether the skip is structurally rescuable.</param>
    public sealed record SkipOccurrence(
        RuntimeUnitID Unit,
        TraceEventID SkipEvent,
        int CausalDepth,
        OwnershipClassification Ownership,
        InstanceID? HostInstance,
        string SkipCause,
        bool Rescuable
    )
    {
        /// <summary>
        /// The skip event's causal depth. Validated non-negative at construction.
        /// </summary>
        public int CausalDepth { get; } = RequireDepth(CausalDepth);

        /// <summary>
        /// The owning host instance. Validated at construction: non-null exactly when the ownership
        /// is player-owned.
        /// </summary>
        public InstanceID? HostInstance { get; } = RequireHostPairing(Ownership, HostInstance);

        /// <summary>
        /// The explicit skip cause. Validated non-empty at construction.
        /// </summary>
        public string SkipCause { get; } = RequireCause(SkipCause);

        /// <summary>
        /// Validates that the causal depth is non-negative.
        /// </summary>
        /// <param name="causalDepth">The candidate depth.</param>
        /// <returns>The depth unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the depth is negative.</exception>
        private static int RequireDepth(int causalDepth)
        {
            if (causalDepth < 0)
                throw new ArgumentException("An occurrence requires a non-negative causal depth.", nameof(causalDepth));

            return causalDepth;
        }

        /// <summary>
        /// Validates that the host instance is present exactly when the unit is player-owned.
        /// </summary>
        /// <param name="ownership">The unit's ownership classification.</param>
        /// <param name="hostInstance">The candidate host instance.</param>
        /// <returns>The host instance unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when presence and ownership disagree.</exception>
        private static InstanceID? RequireHostPairing(
            OwnershipClassification ownership,
            InstanceID? hostInstance)
        {
            if (ownership == OwnershipClassification.PlayerOwned && hostInstance == null)
                throw new ArgumentException("A player-owned occurrence requires a host instance.", nameof(hostInstance));

            if (ownership == OwnershipClassification.CoreOwned && hostInstance != null)
                throw new ArgumentException("A Core-owned occurrence carries no host instance.", nameof(hostInstance));

            return hostInstance;
        }

        /// <summary>
        /// Validates that the skip cause is present and non-empty.
        /// </summary>
        /// <param name="skipCause">The candidate skip cause.</param>
        /// <returns>The cause unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the cause is null or empty.</exception>
        private static string RequireCause(string skipCause)
        {
            if (string.IsNullOrEmpty(skipCause))
                throw new ArgumentException("A skip occurrence requires an explicit skip cause.", nameof(skipCause));

            return skipCause;
        }
    }
}