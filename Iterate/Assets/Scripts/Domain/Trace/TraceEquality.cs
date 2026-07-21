using System.Collections.Generic;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// Structural-equality helper for the trace-record contract. The record types carry read-only lists
    /// whose default reference equality would make two independently-built-but-equal records unequal; this
    /// element-wise comparison lets replay-through-storage assert full record equality across an
    /// independently built value. Kept local to the Trace namespace rather than shared with the
    /// Determinism twin so neither namespace depends on the other.
    /// </summary>
    internal static class TraceEquality
    {
        /// <summary>
        /// Compares two read-only lists element-wise using the default equality comparer for the element
        /// type.
        /// </summary>
        /// <typeparam name="TItem">The element type.</typeparam>
        /// <param name="left">The left list.</param>
        /// <param name="right">The right list.</param>
        /// <returns>True when both are the same instance, both null, or equal element-wise.</returns>
        public static bool ListEquals<TItem>(IReadOnlyList<TItem> left, IReadOnlyList<TItem> right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;
            if (left.Count != right.Count)
                return false;

            EqualityComparer<TItem> comparer = EqualityComparer<TItem>.Default;
            for (int index = 0; index < left.Count; index++)
            {
                if (!comparer.Equals(left[index], right[index]))
                    return false;
            }

            return true;
        }
    }
}