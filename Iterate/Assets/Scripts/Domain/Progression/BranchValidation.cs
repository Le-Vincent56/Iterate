using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Everything wrong with a draft, reported together rather than first-failure-only, so the
    /// interface can list every conflict at once. Under capacity is legal and is not a conflict.
    /// </summary>
    /// <param name="SelectedCount">How many instances the draft selects.</param>
    /// <param name="Capacity">The capacity the draft is judged against.</param>
    /// <param name="IsOverCapacity">Whether the draft selects more than the capacity allows.</param>
    /// <param name="MissingRequired">Required content IDs the draft does not satisfy.</param>
    /// <param name="QuarantinedPresent">Selected instances whose content is Quarantined.</param>
    public sealed record BranchValidation(
        int SelectedCount,
        int Capacity,
        bool IsOverCapacity,
        IReadOnlyList<string> MissingRequired,
        IReadOnlyList<InstanceID> QuarantinedPresent
    )
    {
        /// <summary>
        /// Whether the draft may be confirmed.
        /// </summary>
        public bool IsValid => !IsOverCapacity && MissingRequired.Count == 0 && QuarantinedPresent.Count == 0;
    }
}