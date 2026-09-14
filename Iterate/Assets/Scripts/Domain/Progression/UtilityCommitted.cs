using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// A Utility committed to a Process by a transaction.
    /// </summary>
    /// <param name="Commitment">The commitment's identity.</param>
    /// <param name="DefinitionID">The Utility definition's identity.</param>
    /// <param name="TargetProcess">The Process it applies to.</param>
    public sealed record UtilityCommitted(InstanceID Commitment, string DefinitionID, ProcessID TargetProcess) : TransactionConsequence;
}