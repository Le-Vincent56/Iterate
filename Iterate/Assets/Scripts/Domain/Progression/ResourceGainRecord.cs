using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One resource gain a Process actually credited, with the archive that triggered it — so a Byte
    /// that appeared mid-Process can be traced to the Dependency and the item behind it.
    /// </summary>
    /// <param name="Origin">The Dependency instance that gained it.</param>
    /// <param name="DefinitionID">The Dependency definition's identity.</param>
    /// <param name="Resource">The resource token gained.</param>
    /// <param name="Amount">How much was gained.</param>
    /// <param name="ArchivedInstance">The instance whose archiving triggered it.</param>
    public sealed record ResourceGainRecord(
        InstanceID Origin,
        string DefinitionID,
        string Resource,
        int Amount,
        InstanceID ArchivedInstance
    );
}