using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// A Repository item deleted by a transaction.
    /// </summary>
    /// <param name="Item">The deleted instance.</param>
    /// <param name="DefinitionID">The content definition's identity.</param>
    public sealed record ItemDeleted(InstanceID Item, string DefinitionID) : TransactionConsequence;
}