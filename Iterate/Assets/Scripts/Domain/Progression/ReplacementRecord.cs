using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Records one entry's item being swapped for a derived record under the same identity — the shape
    /// a Patch attachment takes. The entry keeps its identity, origin, protection and suffix.
    /// </summary>
    /// <param name="InstanceID">The instance whose item was replaced.</param>
    /// <param name="DefinitionID">The definition's stable identity, unchanged by the replacement.</param>
    public sealed record ReplacementRecord(
        InstanceID InstanceID,
        string DefinitionID
    ) : RepositoryRecord(InstanceID, DefinitionID);
}