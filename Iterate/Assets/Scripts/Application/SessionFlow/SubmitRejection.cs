namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Why a result could not be submitted. No Domain operation owns submission, so this is the one
    /// refusal vocabulary Session Flow declares itself.
    /// </summary>
    public enum SubmitRejection
    {
        /// <summary>
        /// The result was submitted.
        /// </summary>
        None,

        /// <summary>
        /// No result is under review, or the one under review was discarded.
        /// </summary>
        NoCurrentResult,

        /// <summary>
        /// The result did not reach Pass.
        /// </summary>
        BelowPass,

        /// <summary>
        /// The Process makes its executions mandatory and some remain.
        /// </summary>
        MandatoryExecutionsRemain,
    }
}