using Iterate.Domain.Values;

namespace Iterate.Application.Persistence
{
    /// <summary>
    /// One Utility commitment as a checkpoint holds it. The target Process is part of the commitment's
    /// identity: the same Utility may be committed to two Processes but never twice to one.
    /// </summary>
    /// <param name="Instance">The commitment's identity.</param>
    /// <param name="Definition">The Utility definition's stable identity.</param>
    /// <param name="TargetProcess">The Process the Utility applies to.</param>
    /// <param name="OfferID">The shop offer that sold it.</param>
    public sealed record CheckpointUtility(
        InstanceID Instance,
        string Definition,
        ProcessID TargetProcess,
        string OfferID
    );
}