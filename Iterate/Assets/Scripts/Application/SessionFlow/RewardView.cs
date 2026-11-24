using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The submitted Process's rewards still to resolve. Every list is a copy.
    /// </summary>
    /// <param name="IsComplete">Whether every reward is resolved.</param>
    /// <param name="Pending">The components still to choose.</param>
    /// <param name="OpenGrants">The granted Patches still to attach.</param>
    public sealed record RewardView(bool IsComplete, IReadOnlyList<PendingRewardView> Pending, IReadOnlyList<InstanceID> OpenGrants);
}