using System;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The derived state of one instance within a Branch configuration, as flags rather than a single
    /// value, because an instance is routinely several of these at once — Selected and Required and
    /// Recommended, say. Derived on demand from the draft, the constraints and the item's tags; never
    /// stored, so it cannot go stale.
    /// </summary>
    [Flags]
    public enum BranchInstanceState
    {
        /// <summary>
        /// The instance is not in the Repository snapshot this configuration was opened against.
        /// </summary>
        None = 0,

        /// <summary>
        /// The instance may be selected for this Branch.
        /// </summary>
        Eligible = 1,

        /// <summary>
        /// The instance is selected in the draft.
        /// </summary>
        Selected = 2,

        /// <summary>
        /// The instance satisfies a Required content rule and cannot be excluded.
        /// </summary>
        Required = 4,

        /// <summary>
        /// The instance's content is Quarantined for this Process.
        /// </summary>
        Quarantined = 8,

        /// <summary>
        /// The instance carries a tag the Process marks as recommended. Advisory only.
        /// </summary>
        Recommended = 16,

        /// <summary>
        /// The instance carries a tag the Process marks as caution. Advisory only; still legal.
        /// </summary>
        Caution = 32
    }
}