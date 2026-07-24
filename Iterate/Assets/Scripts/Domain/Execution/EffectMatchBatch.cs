using System;
using System.Collections.Generic;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One boundary's captured sibling batch: the qualified effects already in resolution order, the
    /// near-misses, the re-applications — consumed selected-host effects whose recorded host matches
    /// the occurrence and whose qualifiers pass — and the creators: qualified added-execution effects
    /// awaiting commitment. Captured complete before the first sibling resolves; mid-batch
    /// resolutions can create descendants, never new siblings.
    /// </summary>
    /// <param name="Qualified">The qualified effects in resolution order; non-null.</param>
    /// <param name="NearMisses">The near-misses at this boundary; non-null.</param>
    /// <param name="Reapplications">The selected-host re-applications in resolution order; non-null.</param>
    /// <param name="Creators">The qualified added-execution creators in resolution order; non-null.</param>
    public sealed record EffectMatchBatch(
        IReadOnlyList<ActiveEffect> Qualified,
        IReadOnlyList<EffectNearMiss> NearMisses,
        IReadOnlyList<ActiveEffect> Reapplications,
        IReadOnlyList<ActiveEffect> Creators
    )
    {
        /// <summary>
        /// The shared immutable no-match batch, returned whenever a boundary offers nothing — keeping
        /// the zero-Dependency fast path allocation-free.
        /// </summary>
        public static EffectMatchBatch Empty { get; } = new EffectMatchBatch(
            Array.Empty<ActiveEffect>(),
            Array.Empty<EffectNearMiss>(),
            Array.Empty<ActiveEffect>(),
            Array.Empty<ActiveEffect>());

        /// <summary>
        /// The qualified effects in resolution order. Validated non-null at construction.
        /// </summary>
        public IReadOnlyList<ActiveEffect> Qualified { get; } = RequireList(Qualified);

        /// <summary>
        /// The near-misses at this boundary. Validated non-null at construction.
        /// </summary>
        public IReadOnlyList<EffectNearMiss> NearMisses { get; } = RequireList(NearMisses);

        /// <summary>
        /// The selected-host re-applications in resolution order. Validated non-null at construction.
        /// </summary>
        public IReadOnlyList<ActiveEffect> Reapplications { get; } = RequireList(Reapplications);

        /// <summary>
        /// The qualified added-execution creators in resolution order. Validated non-null at
        /// construction.
        /// </summary>
        public IReadOnlyList<ActiveEffect> Creators { get; } = RequireList(Creators);

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