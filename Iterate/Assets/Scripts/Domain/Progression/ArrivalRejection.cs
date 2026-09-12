namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The typed reason an arrival moment could not fire. Distinct from the Process-creation
    /// vocabulary: these are sequencing faults at a moment, not faults in the authored Process.
    /// </summary>
    public enum ArrivalRejection
    {
        /// <summary>
        /// No rejection; the moment fired.
        /// </summary>
        None,

        /// <summary>
        /// The configuration declares no load after that execution.
        /// </summary>
        NoSuchMoment,

        /// <summary>
        /// The moment has already fired; a scripted moment fires exactly once.
        /// </summary>
        MomentAlreadyFired,

        /// <summary>
        /// The moment follows the Process's final execution, and nothing arrives after it.
        /// </summary>
        AfterFinalExecution,

        /// <summary>
        /// The moment names content the Repository cannot supply an unexposed instance of.
        /// </summary>
        ContentMissing
    }
}