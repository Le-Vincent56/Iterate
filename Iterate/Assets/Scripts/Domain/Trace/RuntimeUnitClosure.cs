using System;
using System.Collections.Generic;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The evidence captured when a runtime unit completes: the disposition, preserving an original
    /// disposition separately when a transformation occurred, the primary-operation link, the requests
    /// the unit created, the closure status, and the safety status. When an original disposition is
    /// present it must differ from the final disposition — a transformation that changed nothing is a
    /// defect. Equality is structural over the created-request list.
    /// </summary>
    public sealed record RuntimeUnitClosure
    {
        /// <summary>
        /// The disposition before a transformation, or null when no transformation occurred; when
        /// present it differs from the final disposition.
        /// </summary>
        public EventDisposition? OriginalDisposition { get; }

        /// <summary>
        /// The final disposition of the unit.
        /// </summary>
        public EventDisposition FinalDisposition { get; }

        /// <summary>
        /// The primary operation event of the unit, or null when it had none.
        /// </summary>
        public TraceEventID? PrimaryOperationEvent { get; }

        /// <summary>
        /// The identities of the requests this unit created, in declared order; never null, possibly
        /// empty, each entry non-empty.
        /// </summary>
        public IReadOnlyList<string> CreatedRequestIdentities { get; }

        /// <summary>
        /// The terminal condition under which the unit closed.
        /// </summary>
        public UnitClosureStatus Status { get; }

        /// <summary>
        /// The safety status at closure.
        /// </summary>
        public SafetyStatus SafetyStatus { get; }

        public RuntimeUnitClosure(
            EventDisposition? originalDisposition,
            EventDisposition finalDisposition,
            TraceEventID? primaryOperationEvent,
            IReadOnlyList<string> createdRequestIdentities,
            UnitClosureStatus status,
            SafetyStatus safetyStatus
        )
        {
            if (createdRequestIdentities == null)
                throw new ArgumentException("A unit closure requires a created-request list.", nameof(createdRequestIdentities));
            
            for (int index = 0; index < createdRequestIdentities.Count; index++)
            {
                if (string.IsNullOrEmpty(createdRequestIdentities[index]))
                    throw new ArgumentException("A created-request entry may not be empty.", nameof(createdRequestIdentities));
            }
            
            if (originalDisposition == finalDisposition)
                throw new ArgumentException("A disposition transformation must change the disposition.", nameof(originalDisposition));

            OriginalDisposition = originalDisposition;
            FinalDisposition = finalDisposition;
            PrimaryOperationEvent = primaryOperationEvent;
            CreatedRequestIdentities = createdRequestIdentities;
            Status = status;
            SafetyStatus = safetyStatus;
        }

        /// <summary>
        /// Structural value equality: the dispositions, primary-operation link, status, and safety status
        /// plus element-wise created-request comparison.
        /// </summary>
        /// <param name="other">The closure to compare against.</param>
        /// <returns>True when every field and the created requests are equal.</returns>
        public bool Equals(RuntimeUnitClosure other)
        {
            if (other == null) return false;

            return OriginalDisposition == other.OriginalDisposition
                && FinalDisposition == other.FinalDisposition
                && PrimaryOperationEvent == other.PrimaryOperationEvent
                && Status == other.Status
                && SafetyStatus == other.SafetyStatus
                && TraceEquality.ListEquals(CreatedRequestIdentities, other.CreatedRequestIdentities);
        }

        /// <summary>
        /// A hash consistent with structural equality, over the final disposition, status, safety status,
        /// and created-request count.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (int)FinalDisposition;
                hash = hash * 31 + (int)Status;
                hash = hash * 31 + (int)SafetyStatus;
                hash = hash * 31 + CreatedRequestIdentities.Count;
                return hash;
            }
        }
    }
}