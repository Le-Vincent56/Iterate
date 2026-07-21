namespace Iterate.Domain.Trace
{
    /// <summary>
    /// Whether the evidence of an execution is complete or carries at least one recorded defect. This is
    /// recorded independently of whether the execution itself completed.
    /// </summary>
    public enum TraceCompletenessStatus
    {
        /// <summary>
        /// The evidence carries no defects.
        /// </summary>
        Complete,

        /// <summary>
        /// The evidence carries at least one defect.
        /// </summary>
        Incomplete
    }
}