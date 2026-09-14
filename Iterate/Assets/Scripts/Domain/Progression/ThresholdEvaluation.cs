using Iterate.Domain.Content;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The tier a Process reached, if any. A successful evaluation with a null tier is the ordinary
    /// below-Pass outcome; a failed one reached no tier because the result was not valid at all.
    /// </summary>
    /// <param name="Succeeded">Whether the result could be evaluated.</param>
    /// <param name="Reached">The highest tier reached, or null.</param>
    /// <param name="Rejection">Why it could not be evaluated; None on success.</param>
    public sealed record ThresholdEvaluation(bool Succeeded, RewardTier? Reached, ThresholdRejection Rejection);
}