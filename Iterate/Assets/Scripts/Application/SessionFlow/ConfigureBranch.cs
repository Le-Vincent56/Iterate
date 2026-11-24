using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Includes an item in, or excludes it from, the Active Branch being configured, or confirms the
    /// configuration and enters the Process.
    /// </summary>
    /// <param name="Action">What to do.</param>
    /// <param name="Item">The item to include or exclude; null to confirm.</param>
    public sealed record ConfigureBranch(BranchAction Action, InstanceID? Item) : ISessionCommand;
}