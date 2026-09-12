using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Records one Branch confirmation: the set that was locked, and any instances the previous
    /// confirmation had held that no longer exist in the Repository. Dropped instances are reported
    /// here rather than silently disappearing, so telemetry can see a deletion reach a Branch.
    /// </summary>
    /// <param name="Eligible">The confirmed eligible instances.</param>
    /// <param name="DroppedInstances">Previously confirmed instances the Repository no longer holds.</param>
    public sealed record BranchConfirmationRecord(
        IReadOnlyList<InstanceID> Eligible,
        IReadOnlyList<InstanceID> DroppedInstances
    );
}