namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// How a selected candidate is treated for later draws — the four outcomes, declared
    /// explicitly so a decision record can never be ambiguous about replacement.
    /// </summary>
    public enum ReplacementBehavior
    {
        /// <summary>
        /// The selected candidate stays eligible for later draws.
        /// </summary>
        RemainsEligible,

        /// <summary>
        /// The selected candidate is removed for the remainder of this decision.
        /// </summary>
        RemovedForThisDecision,

        /// <summary>
        /// The selected candidate is removed from a longer-lived population.
        /// </summary>
        RemovedFromLongerLivedPopulation,

        /// <summary>
        /// The selected candidate is transformed before later draws.
        /// </summary>
        TransformedBeforeLaterDraws
    }
}