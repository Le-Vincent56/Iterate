using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// A Dependency destroyed by a transaction, with the refund it produced.
    /// </summary>
    /// <param name="Dependency">The destroyed instance.</param>
    /// <param name="DefinitionID">The Dependency definition's identity.</param>
    /// <param name="Refund">The Tokens refunded.</param>
    public sealed record DependencyDestroyed(InstanceID Dependency, string DefinitionID, int Refund) : TransactionConsequence;
}