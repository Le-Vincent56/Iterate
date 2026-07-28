namespace Iterate.Domain.Diagnostics
{
    /// <summary>
    /// The outcome of one replay comparison: the recomputed evidence matched the stored evidence, it
    /// diverged at an identified locus, or the candidate inputs did not correspond to the stored ones,
    /// so no exact reproduction was possible.
    /// </summary>
    public enum ReplayComparisonStatus
    {
        /// <summary>
        /// Stored and recomputed records are structurally equal.
        /// </summary>
        Match,

        /// <summary>
        /// Correspondence held, but the recomputed evidence differs from the stored evidence at a
        /// reported area and element.
        /// </summary>
        Diverged,

        /// <summary>
        /// The candidate request does not correspond to the stored execution's inputs, so the stored
        /// execution cannot be exactly reproduced and no evidence comparison was performed. The record
        /// the engine produced under those inputs is withheld rather than reported.
        /// </summary>
        ExactReproductionUnavailable
    }
}