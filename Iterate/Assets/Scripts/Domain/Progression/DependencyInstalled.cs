using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// A Dependency installed by a transaction.
    /// </summary>
    /// <param name="Dependency">The installed instance.</param>
    /// <param name="DefinitionID">The Dependency definition's identity.</param>
    /// <param name="PricePaid">The Tokens paid.</param>
    public sealed record DependencyInstalled(InstanceID Dependency, string DefinitionID, int PricePaid) : TransactionConsequence;
}