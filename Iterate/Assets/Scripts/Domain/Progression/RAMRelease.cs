using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// RAM released by a destruction.
    /// </summary>
    /// <param name="Dependency">The Dependency instance that released it.</param>
    /// <param name="Amount">The RAM released.</param>
    /// <param name="UsageAfter">The rack usage after the release.</param>
    public sealed record RAMRelease(InstanceID Dependency, int Amount, int UsageAfter) : TransactionConsequence;
}