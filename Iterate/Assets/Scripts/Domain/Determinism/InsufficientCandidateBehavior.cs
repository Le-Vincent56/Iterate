namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// The declared behavior when the captured population cannot satisfy the selection count — the five
    /// values. Population shortfall is a legal runtime fact routed through one of these, never
    /// an exception.
    /// </summary>
    public enum InsufficientCandidateBehavior
    {
        /// <summary>
        /// The decision fails to qualify; no candidate is selected.
        /// </summary>
        FailToQualify,

        /// <summary>
        /// All remaining candidates are selected.
        /// </summary>
        SelectAllRemainingCandidates,

        /// <summary>
        /// The selection count is reduced to the available population.
        /// </summary>
        ReduceSelectionCount,

        /// <summary>
        /// The decision is cancelled; no draws occur.
        /// </summary>
        CancelTheDecision,

        /// <summary>
        /// A declared fallback resolves the decision.
        /// </summary>
        UseDeclaredFallback
    }
}