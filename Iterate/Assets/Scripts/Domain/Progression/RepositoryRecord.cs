using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The common shape of a Repository transaction record. The Repository keeps one ordered sequence
    /// of these rather than a list per operation, so the order in which acquisitions, deletions and
    /// replacements happened is preserved. Progression records are distinct from execution evidence:
    /// they describe what the player's holdings did, not what a runtime unit did.
    /// </summary>
    /// <param name="InstanceID">The instance the transaction concerned.</param>
    /// <param name="DefinitionID">The definition's stable identity.</param>
    public abstract record RepositoryRecord(InstanceID InstanceID, string DefinitionID);
}