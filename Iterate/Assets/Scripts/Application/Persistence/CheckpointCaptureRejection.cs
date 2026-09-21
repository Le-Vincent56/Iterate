namespace Iterate.Application.Persistence
{
    /// <summary>
    /// Why a Session could not be captured as a checkpoint.
    /// </summary>
    public enum CheckpointCaptureRejection
    {
        /// <summary>
        /// No rejection; the Session was captured.
        /// </summary>
        None,

        /// <summary>
        /// A Branch configuration is open, so the Session is mid-edit and has no settled state to save.
        /// </summary>
        BranchConfigurationInProgress
    }
}