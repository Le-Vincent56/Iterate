using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// One installed Dependency: its identity, its definition, the RAM it holds, and whether it is the
    /// permanent starter.
    /// </summary>
    /// <param name="Instance">The Dependency's instance identity.</param>
    /// <param name="DefinitionID">Its definition.</param>
    /// <param name="RAM">The RAM it holds.</param>
    /// <param name="Starter">Whether it is the permanent starter.</param>
    public sealed record DependencyView(
        InstanceID Instance,
        string DefinitionID,
        int RAM,
        bool Starter
    );
}