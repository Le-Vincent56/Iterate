namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The canonical tri-state safety status surfaced in unit and execution records. The
    /// limit-approached state is diagnostic only and never changes mechanics.
    /// </summary>
    public enum SafetyStatus
    {
        /// <summary>
        /// No safety ceiling is near or breached.
        /// </summary>
        Normal,

        /// <summary>
        /// A safety ceiling is near; diagnostic only.
        /// </summary>
        SafetyLimitApproached,

        /// <summary>
        /// A safety ceiling was breached and the execution aborted.
        /// </summary>
        SafetyAborted
    }
}