namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Why a Session ended.
    /// </summary>
    public enum SessionEndReason
    {
        /// <summary>
        /// The Session has not ended.
        /// </summary>
        None,

        /// <summary>
        /// The player submitted the final Process.
        /// </summary>
        Completed,

        /// <summary>
        /// A Process failed and the lifecycle ended the Session.
        /// </summary>
        Failed,

        /// <summary>
        /// The player abandoned the Session.
        /// </summary>
        Abandoned,
    }
}