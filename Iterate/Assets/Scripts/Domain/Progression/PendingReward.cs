using Iterate.Domain.Content;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// A choice the player still owes the resolution.
    /// </summary>
    /// <param name="ComponentIndex">The component's authored index.</param>
    /// <param name="Component">The authored component.</param>
    /// <param name="Pool">The pool to choose from.</param>
    public sealed record PendingReward(int ComponentIndex, RewardComponent Component, PoolDefinition Pool);
}