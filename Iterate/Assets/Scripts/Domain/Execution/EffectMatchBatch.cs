using System;
using System.Collections.Generic;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One boundary's captured sibling batch: the qualified effects already in resolution order and
    /// the near-misses. Captured complete before the first sibling resolves; mid-batch resolutions
    /// can create descendants, never new siblings.
    /// </summary>
    /// <param name="Qualified">The qualified effects in resolution order; non-null.</param>
    /// <param name="NearMisses">The near-misses at this boundary; non-null.</param>
    public sealed record EffectMatchBatch(
        IReadOnlyList<ActiveEffect> Qualified,
        IReadOnlyList<EffectNearMiss> NearMisses
    )
    {
        /// <summary>
        /// The shared immutable no-match batch, returned whenever a boundary offers nothing — keeping
        /// the zero-Dependency fast path allocation-free.
        /// </summary>
        public static EffectMatchBatch Empty { get; } = new EffectMatchBatch(
            Array.Empty<ActiveEffect>(),
            Array.Empty<EffectNearMiss>());

        /// <summary>
        /// The qualified effects in resolution order. Validated non-null at construction.
        /// </summary>
        public IReadOnlyList<ActiveEffect> Qualified { get; } = RequireList(Qualified);

        /// <summary>
        /// The near-misses at this boundary. Validated non-null at construction.
        /// </summary>
        public IReadOnlyList<EffectNearMiss> NearMisses { get; } = RequireList(NearMisses);

        /// <summary>
        /// Validates that a batch list is present.
        /// </summary>
        /// <typeparam name="T">The list's element type.</typeparam>
        /// <param name="list">The candidate list.</param>
        /// <returns>The list unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the list is null.</exception>
        private static IReadOnlyList<T> RequireList<T>(IReadOnlyList<T> list)
        {
            if (list == null)
                throw new ArgumentException("A match batch requires its lists.", nameof(list));

            return list;
        }
    }
}