namespace Iterate.Domain.Trace
{
    /// <summary>
    /// Whether an execution ran to completion or was aborted by a safety intervention. This is recorded
    /// independently of whether the trace of it is complete.
    /// </summary>
    public enum ExecutionCompletionStatus
    {
        /// <summary>
        /// The execution ran to completion.
        /// </summary>
        Completed,

        /// <summary>
        /// The execution was aborted by a safety intervention.
        /// </summary>
        SafetyAborted
    }
}