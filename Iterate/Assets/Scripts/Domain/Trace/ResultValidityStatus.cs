namespace Iterate.Domain.Trace
{
    /// <summary>
    /// Whether an execution's result may be trusted. A result is valid only when the execution completed
    /// and its trace is complete; this is recorded independently of the final quantities, which are
    /// preserved regardless.
    /// </summary>
    public enum ResultValidityStatus
    {
        /// <summary>
        /// The result may be trusted.
        /// </summary>
        Valid,

        /// <summary>
        /// The result may not be trusted.
        /// </summary>
        Invalid
    }
}