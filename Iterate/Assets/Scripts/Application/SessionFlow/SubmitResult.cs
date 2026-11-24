using Iterate.Domain.Progression;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The outcome of submitting a result: on success, the reward resolution it produced.
    /// </summary>
    /// <param name="Succeeded">Whether the result was submitted.</param>
    /// <param name="Rejection">Why not; None on success.</param>
    /// <param name="Rewards">The reward resolution; null when refused.</param>
    public sealed record SubmitResult(bool Succeeded, SubmitRejection Rejection, RewardResolution Rewards);
}