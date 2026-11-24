namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// What the Session does after a Process fails.
    /// </summary>
    public enum ProcessFailureDisposition
    {
        /// <summary>
        /// The Session ends as failed.
        /// </summary>
        EndSession,

        /// <summary>
        /// The same Process restarts from its initial scripted state; Session acquisitions are kept.
        /// </summary>
        RestartProcess,

        /// <summary>
        /// The Session returns to Branch configuration for the same Process, seeded from the
        /// confirmed Branch.
        /// </summary>
        ReturnToBranchConfiguration,
    }
}