using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// A Repository item acquired by a transaction.
    /// </summary>
    /// <param name="Item">The acquired instance.</param>
    /// <param name="DefinitionID">The content definition's identity.</param>
    /// <param name="Origin">How it was acquired.</param>
    public sealed record ItemAcquired(InstanceID Item, string DefinitionID, AcquisitionOrigin Origin) : TransactionConsequence;
}