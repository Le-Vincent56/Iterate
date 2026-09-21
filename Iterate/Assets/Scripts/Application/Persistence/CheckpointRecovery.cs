namespace Iterate.Application.Persistence
{
    /// <summary>
    /// How a restored Session resumes at its Process. Derived from the Process's authored role, which
    /// is the datum the design keys recovery on — not from the Buffer-load policy, which only happens
    /// to correlate with it in the content shipped today.
    /// </summary>
    public enum CheckpointRecovery
    {
        /// <summary>
        /// The Process is replayed from its scripted start; there is no Branch to reconfigure.
        /// </summary>
        ScriptedRestart,

        /// <summary>
        /// The player returns to the Branch configuration screen before re-entering the Process.
        /// </summary>
        BranchConfiguration
    }
}