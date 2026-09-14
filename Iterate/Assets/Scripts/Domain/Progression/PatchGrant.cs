using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// A Patch awarded but not yet socketed. There is no unattached Patch state in the Repository
    /// so a reward's Patch waits here as a grant until an attachment consumes it. A cached grant is one
    /// the player may hold indefinitely and does not hold its reward open.
    /// </summary>
    /// <param name="GrantIdentity">The grant's own instance identity.</param>
    /// <param name="Definition">The frozen Patch definition granted.</param>
    /// <param name="Cached">Whether the grant may be held indefinitely.</param>
    /// <param name="Basis">The reward component the grant came from.</param>
    public sealed record PatchGrant(
        InstanceID GrantIdentity,
        PatchDefinition Definition,
        bool Cached,
        string Basis
    );
}