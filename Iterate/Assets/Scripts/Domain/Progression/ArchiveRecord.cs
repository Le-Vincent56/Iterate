using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Records one item becoming unavailable for the rest of the Process. It returns to the Repository
    /// at Process cleanup, so this is never a deletion.
    /// </summary>
    /// <param name="InstanceID">The archived instance.</param>
    /// <param name="DefinitionID">The definition's stable identity.</param>
    /// <param name="WasIncoming">Whether the item was archived from outside the Buffer during overflow.</param>
    public sealed record ArchiveRecord(
        InstanceID InstanceID,
        string DefinitionID,
        bool WasIncoming
    ) : BufferRecord(InstanceID, DefinitionID);
}