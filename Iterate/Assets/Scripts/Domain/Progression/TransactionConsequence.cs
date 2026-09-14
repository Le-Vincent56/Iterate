namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One thing a committed transaction did. A transaction records its consequences in commit order,
    /// so a reader reconstructs what happened without re-deriving it from the holders.
    /// </summary>
    public abstract record TransactionConsequence;
}