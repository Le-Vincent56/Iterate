namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The terminal condition under which a runtime unit closed. Every opened unit closes exactly once,
    /// including on abort — a branch termination is a closure, not an absence of one.
    /// </summary>
    public enum UnitClosureStatus
    {
        /// <summary>
        /// The unit completed normally.
        /// </summary>
        NormalCompletion,

        /// <summary>
        /// The unit's branch was terminated before normal completion.
        /// </summary>
        BranchTerminated,

        /// <summary>
        /// The unit was cancelled after a legal request.
        /// </summary>
        Cancelled,

        /// <summary>
        /// The unit closed under a safety intervention.
        /// </summary>
        SafetyIntervention,

        /// <summary>
        /// The unit closed with malformed evidence recorded against it.
        /// </summary>
        MalformedEvidence
    }
}