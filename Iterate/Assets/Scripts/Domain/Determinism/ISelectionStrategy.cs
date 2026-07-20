using System.Collections.Generic;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// The internal contract for a selection method's draw logic. Implementations are internal sealed and
    /// dispatched from the <see cref="SelectionMethod"/> enum; there is no public extension point — a
    /// seventh method is a canon revision, not a subclass. A strategy appends its
    /// per-draw records to the draws buffer and, for a shuffle or ordering, its full permutation to the
    /// result-permutation buffer, returning the selected outcome.
    /// </summary>
    internal interface ISelectionStrategy
    {
        /// <summary>
        /// Resolves the selection over the snapshot using the per-decision stream.
        /// </summary>
        /// <param name="request">The decision request.</param>
        /// <param name="snapshot">The captured candidate snapshot in canonical order.</param>
        /// <param name="stream">The per-decision PCG stream.</param>
        /// <param name="effectiveCount">The number of draws to make.</param>
        /// <param name="draws">The buffer the strategy appends per-draw records to.</param>
        /// <param name="resultPermutation">The buffer the strategy fills with the full permutation for a shuffle or ordering; left empty otherwise.</param>
        /// <returns>The decision outcome with disposition Selected; the pipeline may reclassify it.</returns>
        DecisionOutcome Select(
            DecisionRequest request,
            CandidateSnapshot snapshot,
            Pcg32 stream,
            int effectiveCount,
            List<DrawRecord> draws,
            List<string> resultPermutation
        );
    }
}