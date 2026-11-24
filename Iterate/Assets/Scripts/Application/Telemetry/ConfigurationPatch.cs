using Iterate.Domain.Values;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// A Patch attached to an installed Instruction in a configuration snapshot.
    /// </summary>
    /// <param name="Patch">The Patch instance.</param>
    /// <param name="DefinitionID">The Patch definition.</param>
    /// <param name="Host">The host Instruction instance.</param>
    /// <param name="Socket">The socket the Patch occupies.</param>
    public sealed record ConfigurationPatch(
        InstanceID Patch,
        string DefinitionID,
        InstanceID Host,
        int Socket
    );
}