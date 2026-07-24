namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The outcome of offering a closed host to the pending added-execution tracker.
    /// </summary>
    public enum PendingResolution
    {
        /// <summary>
        /// No pending request was locked to that host.
        /// </summary>
        None,

        /// <summary>
        /// The pending request's locked host closed successfully; the request becomes that unit's
        /// immediate descendant.
        /// </summary>
        Matured,

        /// <summary>
        /// The pending request's locked host closed unsuccessfully; the request is cancelled and its
        /// consumed allowance stays consumed.
        /// </summary>
        Cancelled
    }
}