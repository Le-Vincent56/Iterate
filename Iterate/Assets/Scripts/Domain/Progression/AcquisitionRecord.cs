using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Records one item entering the Repository, with the origin and the suffix it was given.
    /// </summary>
    /// <param name="InstanceID">The acquired instance.</param>
    /// <param name="DefinitionID">The definition's stable identity.</param>
    /// <param name="Origin">How the item was acquired.</param>
    /// <param name="Suffix">The per-definition suffix allocated to it.</param>
    public sealed record AcquisitionRecord(
        InstanceID InstanceID,
        string DefinitionID,
        AcquisitionOrigin Origin,
        int Suffix
    ) : RepositoryRecord(InstanceID, DefinitionID);
}