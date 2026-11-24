using Iterate.Domain.Compilation;
using Iterate.Domain.Values;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// One source line in a configuration snapshot, by data identity.
    /// </summary>
    /// <param name="Line">The one-based line number.</param>
    /// <param name="Kind">What occupies the line.</param>
    /// <param name="Item">The installed instance, or null for a Core or empty line.</param>
    /// <param name="DefinitionID">The installed definition, the Core line's identity, or null when empty.</param>
    /// <param name="Structure">The Structure the line belongs to, or null.</param>
    public sealed record ConfigurationLine(
        int Line,
        SourceSlotKind Kind,
        InstanceID? Item,
        string DefinitionID,
        InstanceID? Structure
    );
}