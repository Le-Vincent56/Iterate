namespace Iterate.Domain.Diagnostics
{
    /// <summary>
    /// The areas of an execution record a replay comparison walks, declared in the order it walks them.
    /// The order is contract, not presentation: the first area to differ is the divergence that gets
    /// reported, so reordering these members changes what a comparison says. Events precede units
    /// because an event index is the finest-grained chronology available, which is what CAB-PRES-112
    /// means by the earliest difference, and every unit-visible difference surfaces through its events
    /// first when both differ. Completion status precedes safety status because the record constructor
    /// cross-checks the pair, so a completion difference always co-occurs with a safety difference.
    /// The three derived identity histories are walked even though a freeze-produced record derives
    /// them from its units and events: the record constructor does not re-validate the derived lists,
    /// so a defectively-stored record can differ there, and a diagnostic must not assume a
    /// well-formedness its own constructor never guaranteed.
    /// </summary>
    public enum ReplayDivergenceArea
    {
        /// <summary>
        /// The chronological event records.
        /// </summary>
        Events,

        /// <summary>
        /// The runtime unit records in identity order.
        /// </summary>
        Units,

        /// <summary>
        /// The canonical-traversal units in start order.
        /// </summary>
        TraversalOrder,

        /// <summary>
        /// The derived threshold-event history.
        /// </summary>
        ThresholdHistory,

        /// <summary>
        /// The derived Process-counter event history.
        /// </summary>
        CounterHistory,

        /// <summary>
        /// The missing-evidence ledger.
        /// </summary>
        Defects,

        /// <summary>
        /// The final register state.
        /// </summary>
        FinalState,

        /// <summary>
        /// The high-water safety tallies.
        /// </summary>
        SafetyCounts,

        /// <summary>
        /// Whether the execution completed or was aborted.
        /// </summary>
        CompletionStatus,

        /// <summary>
        /// The safety status of the execution.
        /// </summary>
        SafetyStatus
    }
}