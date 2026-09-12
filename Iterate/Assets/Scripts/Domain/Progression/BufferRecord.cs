using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The common shape of a Buffer transaction record. The Buffer keeps one ordered sequence of
    /// these, so the order in which admissions, overflows, archives and consumptions happened is
    /// preserved for the trace and for the Process's own reconstruction.
    /// </summary>
    /// <param name="InstanceID">The instance the transaction concerned.</param>
    /// <param name="DefinitionID">The definition's stable identity.</param>
    public abstract record BufferRecord(InstanceID InstanceID, string DefinitionID);
}