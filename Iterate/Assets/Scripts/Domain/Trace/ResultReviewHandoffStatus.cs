namespace Iterate.Domain.Trace
{
    /// <summary>
    /// Whether an execution's result is eligible to be handed to Result Review, or blocked. Eligibility
    /// follows from result validity; the completion decision itself belongs to Session Flow.
    /// </summary>
    public enum ResultReviewHandoffStatus
    {
        /// <summary>
        /// The result is eligible for Result Review.
        /// </summary>
        Eligible,

        /// <summary>
        /// The result is blocked from Result Review.
        /// </summary>
        Blocked
    }
}