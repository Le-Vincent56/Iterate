using System;
using System.Collections.Generic;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// One runtime-unit record: its index-stable identity, its opening and closure evidence, its
    /// immediate causal closure of child events, its added descendant units, and its one-based start and
    /// completion order. Start and completion order are independent — a depth-first descendant may
    /// complete before an earlier sibling's successor starts — so the two are validated only as one-based,
    /// never against each other. Equality is structural over both linkage lists.
    /// </summary>
    public sealed record RuntimeUnitRecord
    {
        /// <summary>
        /// The index-stable identity of this unit.
        /// </summary>
        public RuntimeUnitID Identity { get; }

        /// <summary>
        /// The opening evidence; never null.
        /// </summary>
        public RuntimeUnitOpening Opening { get; }

        /// <summary>
        /// The closure evidence; never null.
        /// </summary>
        public RuntimeUnitClosure Closure { get; }

        /// <summary>
        /// The immediate causal closure of events belonging to this unit, in append order; never null.
        /// </summary>
        public IReadOnlyList<TraceEventID> ChildEvents { get; }

        /// <summary>
        /// The added descendant units created by this unit, in mint order; never null.
        /// </summary>
        public IReadOnlyList<RuntimeUnitID> DescendantUnits { get; }

        /// <summary>
        /// The one-based order in which this unit started.
        /// </summary>
        public int StartOrder { get; }

        /// <summary>
        /// The one-based order in which this unit completed.
        /// </summary>
        public int CompletionOrder { get; }

        public RuntimeUnitRecord(
            RuntimeUnitID identity,
            RuntimeUnitOpening opening,
            RuntimeUnitClosure closure,
            IReadOnlyList<TraceEventID> childEvents,
            IReadOnlyList<RuntimeUnitID> descendantUnits,
            int startOrder,
            int completionOrder
        )
        {
            if (startOrder < 1)
                throw new ArgumentException("A start order must be one or greater.", nameof(startOrder));
            
            if (completionOrder < 1)
                throw new ArgumentException("A completion order must be one or greater.", nameof(completionOrder));

            Identity = identity;
            Opening = opening ?? throw new ArgumentException("A unit record requires an opening.", nameof(opening));
            Closure = closure ?? throw new ArgumentException("A unit record requires a closure.", nameof(closure));
            ChildEvents = childEvents ?? throw new ArgumentException("A unit record requires a child-events list.", nameof(childEvents));
            DescendantUnits = descendantUnits ?? throw new ArgumentException("A unit record requires a descendant-units list.", nameof(descendantUnits));
            StartOrder = startOrder;
            CompletionOrder = completionOrder;
        }

        /// <summary>
        /// Structural value equality: the identity, opening, closure, and ordering fields plus
        /// element-wise comparison of both linkage lists.
        /// </summary>
        /// <param name="other">The record to compare against.</param>
        /// <returns>True when every field and both linkage lists are equal.</returns>
        public bool Equals(RuntimeUnitRecord other)
        {
            if (other == null) return false;

            return Identity == other.Identity
                && Opening == other.Opening
                && Closure == other.Closure
                && StartOrder == other.StartOrder
                && CompletionOrder == other.CompletionOrder
                && TraceEquality.ListEquals(ChildEvents, other.ChildEvents)
                && TraceEquality.ListEquals(DescendantUnits, other.DescendantUnits);
        }

        /// <summary>
        /// A hash consistent with structural equality, over the identity, ordering, and linkage counts.
        /// </summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Identity.GetHashCode();
                hash = hash * 31 + StartOrder;
                hash = hash * 31 + CompletionOrder;
                hash = hash * 31 + ChildEvents.Count;
                hash = hash * 31 + DescendantUnits.Count;
                return hash;
            }
        }
    }
}