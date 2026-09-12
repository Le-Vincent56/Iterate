using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Records one item leaving the Repository permanently for the Session. The suffix is recorded as
    /// it was, and is never reissued to a later acquisition.
    /// </summary>
    /// <param name="InstanceID">The deleted instance.</param>
    /// <param name="DefinitionID">The definition's stable identity.</param>
    /// <param name="Suffix">The suffix the deleted entry held.</param>
    public sealed record DeletionRecord(
        InstanceID InstanceID,
        string DefinitionID,
        int Suffix
    ) : RepositoryRecord(InstanceID, DefinitionID);
}