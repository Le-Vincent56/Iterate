namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// How a decision resolved: a normal selection or one of the declared insufficiency dispositions.
    /// The disposition reproduces under the same context and snapshot.
    /// </summary>
    public enum DecisionDisposition
    {
        /// <summary>
        /// Candidates were selected normally.
        /// </summary>
        Selected,

        /// <summary>
        /// The decision failed to qualify; nothing was selected.
        /// </summary>
        FailedToQualify,

        /// <summary>
        /// All remaining candidates were selected.
        /// </summary>
        SelectedAllRemaining,

        /// <summary>
        /// The selection count was reduced to the available population,
        /// </summary>
        ReducedCount,

        /// <summary>
        /// The decision was cancelled; no draws occurred.
        /// </summary>
        Cancelled,

        /// <summary>
        /// A declared fallback resolved the decision.
        /// </summary>
        FallbackResolved
    }
}