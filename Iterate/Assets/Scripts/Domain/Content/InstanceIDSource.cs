using System;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// Allocates <see cref="InstanceID"/>s deterministically: a monotonic counter yielding 1, 2, 3, … in
    /// call order. No randomness is involved, so allocation stays outside the Determinism Service. Each
    /// source is independent — two sources never share state.
    /// </summary>
    public sealed class InstanceIDSource
    {
        /// <summary>
        /// The ordinal of the most recently allocated identity; zero before the first allocation.
        /// </summary>
        private int _lastAllocated;

        /// <summary>
        /// The ordinal of the most recently allocated identity; zero before the first allocation.
        /// </summary>
        public int LastAllocated => _lastAllocated;

        /// <summary>
        /// Rebuilds a source that has already allocated the given number of identities, so the next
        /// allocation continues the sequence rather than restarting it.
        /// </summary>
        /// <param name="lastAllocated">The ordinal of the most recent allocation; zero for a source that has allocated none.</param>
        /// <returns>The restored source, independent of every other.</returns>
        /// <exception cref="ArgumentException">Thrown when the allocation count is negative.</exception>
        public static InstanceIDSource Restore(int lastAllocated)
        {
            if (lastAllocated < 0)
                throw new ArgumentException("A restored allocation count cannot be negative.", nameof(lastAllocated));

            InstanceIDSource source = new();
            source._lastAllocated = lastAllocated;
            return source;
        }
        
        /// <summary>
        /// Allocates the next identity in sequence.
        /// </summary>
        /// <returns>A fresh identity one greater than the previous allocation, starting at 1.</returns>
        public InstanceID Next()
        {
            _lastAllocated++;
            return new InstanceID(_lastAllocated);
        }
    }
}