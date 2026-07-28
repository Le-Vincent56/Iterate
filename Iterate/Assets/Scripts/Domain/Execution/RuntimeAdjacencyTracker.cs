using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The per-execution runtime-adjacency predecessor: the most recent successfully resolved source
    /// execution in completed order, and whether it qualifies an adjacency-observing effect. A
    /// predecessor qualifies only when it is player-owned and its primary operation actually
    /// increased Score. Successful and rescued closures replace the predecessor; skipped, prevented,
    /// cancelled, and failed-to-qualify closures leave the prior predecessor standing, so structural
    /// gaps do not break adjacency while an intervening successful unit — Core-owned, added, or a
    /// player unit that gained no Score — does. Structure events, reactions, modifier stacking, and
    /// source-less boundary interventions have no entry point here: they are transparent to
    /// adjacency by construction rather than by a filtering branch.
    /// </summary>
    public sealed class RuntimeAdjacencyTracker
    {
        /// <summary>
        /// Whether a predecessor has been recorded at all this execution.
        /// </summary>
        private bool _hasPredecessor;

        /// <summary>
        /// Whether the current predecessor is player-owned.
        /// </summary>
        private bool _predecessorIsPlayerOwned;

        /// <summary>
        /// Whether the current predecessor's primary operation actually increased Score.
        /// </summary>
        private bool _predecessorIncreasedScore;

        /// <summary>
        /// Whether the current predecessor satisfies an adjacency-observing effect: a recorded,
        /// player-owned predecessor whose operation actually increased Score.
        /// </summary>
        public bool CurrentPredecessorQualifies => _hasPredecessor && _predecessorIsPlayerOwned && _predecessorIncreasedScore;

        /// <summary>
        /// Records one runtime unit's closure. A resolved or rescued closure becomes the new
        /// predecessor; every other final disposition leaves the prior predecessor unchanged.
        /// </summary>
        /// <param name="finalDisposition">The unit's final disposition.</param>
        /// <param name="ownership">Whether the unit's host is player- or Core-owned.</param>
        /// <param name="actuallyIncreasedScore">Whether the primary operation's actual Score delta was positive.</param>
        public void RecordCompletion(
            EventDisposition finalDisposition,
            OwnershipClassification ownership,
            bool actuallyIncreasedScore
        )
        {
            if (finalDisposition != EventDisposition.Resolved && finalDisposition != EventDisposition.Rescued)
                return;

            _hasPredecessor = true;
            _predecessorIsPlayerOwned = ownership == OwnershipClassification.PlayerOwned;
            _predecessorIncreasedScore = actuallyIncreasedScore;
        }
    }
}