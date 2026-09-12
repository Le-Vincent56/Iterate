using Iterate.Domain.Determinism;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The outcome of one exposure draw: the item drawn, or nothing when the decision was cancelled
    /// for an insufficient population. The decision record is present either way, because a cancelled
    /// decision is evidence the Process must retain (CAB-EVT-790).
    /// </summary>
    /// <param name="Succeeded">Whether an item was drawn.</param>
    /// <param name="Item">The drawn item; null when the decision was cancelled.</param>
    /// <param name="Record">The complete decision record.</param>
    public readonly record struct ExposureDrawResult(
        bool Succeeded,
        RepositoryItem Item,
        RandomDecisionRecord Record
    )
    {
        /// <summary>
        /// Builds the result of a draw that selected an item.
        /// </summary>
        /// <param name="item">The drawn item.</param>
        /// <param name="record">The decision record.</param>
        /// <returns>The successful result.</returns>
        public static ExposureDrawResult Drawn(RepositoryItem item, RandomDecisionRecord record)
        {
            return new ExposureDrawResult(true, item, record);
        }

        /// <summary>
        /// Builds the result of a decision cancelled for an insufficient population.
        /// </summary>
        /// <param name="record">The decision record.</param>
        /// <returns>The cancelled result.</returns>
        public static ExposureDrawResult Cancelled(RandomDecisionRecord record)
        {
            return new ExposureDrawResult(false, null, record);
        }
    }
}