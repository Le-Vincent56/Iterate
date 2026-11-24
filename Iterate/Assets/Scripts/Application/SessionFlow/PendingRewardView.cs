using System.Collections.Generic;
using Iterate.Domain.Content;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// One unresolved reward component and the options it offers.
    /// </summary>
    /// <param name="ComponentIndex">The component's index in the package.</param>
    /// <param name="Kind">The component's kind.</param>
    /// <param name="Options">The content IDs the player may choose from; empty when the component offers no choice.</param>
    public sealed record PendingRewardView(int ComponentIndex, RewardComponentKind Kind, IReadOnlyList<string> Options);
}