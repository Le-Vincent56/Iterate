using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// One owned item: its identity, its definition, and its acquisition suffix.
    /// </summary>
    /// <param name="Instance">The item's instance identity.</param>
    /// <param name="DefinitionID">The item's definition.</param>
    /// <param name="Suffix">The acquisition-order suffix shown with duplicates.</param>
    public sealed record RepositoryItemView(InstanceID Instance, string DefinitionID, int Suffix);
}