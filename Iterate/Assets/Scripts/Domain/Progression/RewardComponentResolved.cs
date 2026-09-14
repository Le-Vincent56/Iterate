namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One reward component resolved, by index into the package's authored component list.
    /// </summary>
    /// <param name="ComponentIndex">The component's authored index.</param>
    /// <param name="Detail">What resolving it produced.</param>
    public sealed record RewardComponentResolved(int ComponentIndex, string Detail) : TransactionConsequence;
}