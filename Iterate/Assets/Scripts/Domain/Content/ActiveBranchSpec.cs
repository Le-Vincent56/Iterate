using System.Collections.Generic;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A Process's Active Branch constraints: the capacity the confirmed Branch may not exceed, the
    /// content it must contain, the content it may not contain, and the tag sets the presentation
    /// layer reads as recommended or caution. Absent on a tutorial Process, which has no Branch
    /// configuration step.
    /// </summary>
    /// <param name="Capacity">The Active Branch capacity for this Process.</param>
    /// <param name="Required">The content IDs the confirmed Branch must contain.</param>
    /// <param name="Quarantined">The content IDs the confirmed Branch may not contain.</param>
    /// <param name="RecommendedTags">The tags marking recommended content.</param>
    /// <param name="CautionTags">The tags marking caution content.</param>
    public sealed record ActiveBranchSpec(
        int Capacity,
        IReadOnlyList<string> Required,
        IReadOnlyList<string> Quarantined,
        IReadOnlyList<string> RecommendedTags,
        IReadOnlyList<string> CautionTags
    );
}