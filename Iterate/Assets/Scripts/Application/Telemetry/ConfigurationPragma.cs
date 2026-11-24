using Iterate.Domain.Values;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// A pending pragma Directive in a configuration snapshot.
    /// </summary>
    /// <param name="Directive">The Directive instance.</param>
    /// <param name="DefinitionID">The Directive definition.</param>
    public sealed record ConfigurationPragma(InstanceID Directive, string DefinitionID);
}