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