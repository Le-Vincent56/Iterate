namespace Iterate.Domain.Progression
{
    /// <summary>
    /// How an installed Dependency was acquired. A reward install pays no price, which is what makes
    /// its destruction refund zero.
    /// </summary>
    public enum DependencyOrigin
    {
        Purchase,
        Reward
    }
}