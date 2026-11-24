using Iterate.Domain.Content;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The execution result under review.
    /// </summary>
    /// <param name="ExecutionIdentity">The execution.</param>
    /// <param name="FinalOutput">The scored final output.</param>
    /// <param name="Valid">Whether the result is valid.</param>
    /// <param name="Reached">The highest threshold reached, or null below Pass.</param>
    /// <param name="ExecutionsRemaining">How many executions remain.</param>
    public sealed record ResultView(
        string ExecutionIdentity,
        int FinalOutput,
        bool Valid,
        RewardTier? Reached,
        int ExecutionsRemaining
    );
}