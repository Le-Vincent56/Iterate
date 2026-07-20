namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// The RANDOM_SELECTION event family emitted across a decision's lifecycle. This leaf
    /// defines the payload values only; storage and lineage belong to the Trace child.
    /// </summary>
    public enum RandomSelectionEventKind
    {
        /// <summary>
        /// The decision was created (RANDOM_DECISION_CREATED).
        /// </summary>
        DecisionCreated,

        /// <summary>
        /// The candidate snapshot was captured (RANDOM_CANDIDATE_SNAPSHOT_CAPTURED).
        /// </summary>
        CandidateSnapshotCaptured,

        /// <summary>
        /// A single draw resolved (RANDOM_DRAW_RESOLVED); the only kind carrying a nonzero draw ordinal.
        /// </summary>
        DrawResolved,

        /// <summary>
        /// The selection completed successfully (RANDOM_SELECTION_COMPLETED).
        /// </summary>
        SelectionCompleted,

        /// <summary>
        /// The selection failed (RANDOM_SELECTION_FAILED).
        /// </summary>
        SelectionFailed,

        /// <summary>
        /// A declared fallback resolved the decision (RANDOM_FALLBACK_RESOLVED).
        /// </summary>
        FallbackResolved
    }
}