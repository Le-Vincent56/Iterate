using Iterate.Domain.Values;

namespace Iterate.Application.Persistence
{
    /// <summary>
    /// A Patch attached to a Repository item, as a checkpoint holds it: the socket it sits in, the
    /// Patch instance's identity and the definition to resolve it from.
    /// </summary>
    /// <param name="Socket">The one-based socket the Patch occupies.</param>
    /// <param name="Instance">The Patch instance's identity.</param>
    /// <param name="Definition">The Patch definition's stable identity.</param>
    public sealed record CheckpointAttachment(int Socket, InstanceID Instance, string Definition);
}