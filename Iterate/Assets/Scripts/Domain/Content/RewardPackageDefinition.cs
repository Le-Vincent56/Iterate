using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A frozen reward package: the ordered components a Process awards. Document order is
    /// resolution order, so a Token award, a Repository choice, a RAM increase and a Dependency
    /// choice resolve in exactly the authored sequence. Not a content item.
    /// </summary>
    /// <param name="ID">The package's surrogate-key identity.</param>
    /// <param name="DisplayName">The display name shown to the player.</param>
    /// <param name="Components">The reward components in resolution order.</param>
    public sealed record RewardPackageDefinition(
        RewardPackageID ID,
        string DisplayName,
        IReadOnlyList<RewardComponent> Components
    );
}