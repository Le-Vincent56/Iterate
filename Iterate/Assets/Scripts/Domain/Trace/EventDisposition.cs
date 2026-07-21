namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The approved disposition set for a pending occurrence or primary operation. Disposition is
    /// recorded separately from any numerical result: a resolved zero-delta change and a skipped
    /// change are different facts.
    /// </summary>
    public enum EventDisposition
    {
        /// <summary>
        /// The occurrence resolved.
        /// </summary>
        Resolved,

        /// <summary>
        /// The occurrence failed its qualification rules.
        /// </summary>
        FailedToQualify,

        /// <summary>
        /// The occurrence was skipped.
        /// </summary>
        Skipped,

        /// <summary>
        /// The occurrence was prevented.
        /// </summary>
        Prevented,

        /// <summary>
        /// The occurrence was cancelled after a legal request.
        /// </summary>
        Cancelled,

        /// <summary>
        /// A skipped occurrence was transformed back into execution by a rescue effect.
        /// </summary>
        Rescued
    }
}