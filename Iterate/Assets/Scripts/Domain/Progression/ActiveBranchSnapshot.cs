using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// An immutable view of the Active Branch: the confirmed set, and the draft when a configuration is
    /// open. A snapshot never changes when the draft does.
    /// </summary>
    /// <param name="Confirmed">The confirmed configuration, or null before the first confirmation.</param>
    /// <param name="Draft">The draft's selected instances; empty when no configuration is open.</param>
    /// <param name="HasConfigurationInProgress">Whether a draft exists.</param>
    public sealed record ActiveBranchSnapshot(
        ActiveBranchConfiguration Confirmed,
        IReadOnlyList<InstanceID> Draft,
        bool HasConfigurationInProgress
    );
}