using System.Collections.Generic;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The Active Branch being configured and how the draft stands. Every list is a copy.
    /// </summary>
    /// <param name="SelectedCount">How many items the draft selects.</param>
    /// <param name="Capacity">How many the Branch admits.</param>
    /// <param name="IsOverCapacity">Whether the draft selects too many.</param>
    /// <param name="IsValid">Whether the draft may be confirmed.</param>
    /// <param name="MissingRequired">Required content the draft lacks.</param>
    /// <param name="Items">Every owned item and its state, in acquisition order.</param>
    public sealed record BranchView(
        int SelectedCount,
        int Capacity,
        bool IsOverCapacity,
        bool IsValid,
        IReadOnlyList<string> MissingRequired,
        IReadOnlyList<BranchItemView> Items
    );
}