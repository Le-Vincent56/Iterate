using System.Collections.Generic;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The constraints one Branch configuration is judged against: the effective capacity after Process
    /// setup, the content that must be present, the content that may not be, and the tag sets the
    /// presentation layer reads as advisory. Recommended and Caution never block a confirmation.
    /// </summary>
    /// <param name="Capacity">The effective Active Branch capacity.</param>
    /// <param name="Required">Content IDs the confirmed Branch must contain.</param>
    /// <param name="Quarantined">Content IDs the confirmed Branch may not contain.</param>
    /// <param name="RecommendedTags">Tags marking advisory-recommended content.</param>
    /// <param name="CautionTags">Tags marking advisory-caution content, which stays legal.</param>
    public sealed record BranchConstraints(
        int Capacity,
        IReadOnlyList<string> Required,
        IReadOnlyList<string> Quarantined,
        IReadOnlyList<string> RecommendedTags,
        IReadOnlyList<string> CautionTags
    );
}