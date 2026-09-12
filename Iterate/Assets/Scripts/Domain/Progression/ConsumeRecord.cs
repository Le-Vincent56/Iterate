using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Records one Directive being activated and spent out of the Buffer. Distinct from an archive:
    /// the Directive created its pragma rather than being set aside.
    /// </summary>
    /// <param name="InstanceID">The consumed instance.</param>
    /// <param name="DefinitionID">The definition's stable identity.</param>
    public sealed record ConsumeRecord(
        InstanceID InstanceID,
        string DefinitionID
    ) : BufferRecord(InstanceID, DefinitionID);
}