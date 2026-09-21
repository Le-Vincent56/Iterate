using Iterate.Domain.Values;

namespace Iterate.Application.Persistence
{
    /// <summary>
    /// One outstanding Patch grant as a checkpoint holds it — a Patch awarded but not yet attached to a
    /// host.
    /// </summary>
    /// <param name="Instance">The grant's identity.</param>
    /// <param name="Definition">The Patch definition's stable identity.</param>
    /// <param name="Cached">Whether the grant may be held indefinitely.</param>
    /// <param name="Basis">The reward component the grant came from.</param>
    public sealed record CheckpointGrant(
        InstanceID Instance,
        string Definition,
        bool Cached,
        string Basis
    );
}