using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Records one item taking a Buffer slot, whether it arrived directly, returned from the source,
    /// or was drained in from an overflow queue.
    /// </summary>
    /// <param name="InstanceID">The admitted instance.</param>
    /// <param name="DefinitionID">The definition's stable identity.</param>
    /// <param name="SlotNumber">The slot it took.</param>
    public sealed record AdmissionRecord(
        InstanceID InstanceID,
        string DefinitionID,
        int SlotNumber
    ) : BufferRecord(InstanceID, DefinitionID);
}