using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Records one item arriving to a full Buffer and being held outside it, beginning or extending a
    /// blocking overflow state.
    /// </summary>
    /// <param name="InstanceID">The held instance.</param>
    /// <param name="DefinitionID">The definition's stable identity.</param>
    /// <param name="QueueDepth">How many items are waiting outside, including this one.</param>
    public sealed record OverflowRecord(
        InstanceID InstanceID,
        string DefinitionID,
        int QueueDepth
    ) : BufferRecord(InstanceID, DefinitionID);
}