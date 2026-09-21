namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The outcome of rebuilding a Session from a snapshot. A rejection carries no Session at all:
    /// validation completes before anything is constructed, so there is never a partially restored state
    /// for a caller to mistake for a whole one.
    /// </summary>
    /// <param name="Succeeded">Whether the snapshot became a Session.</param>
    /// <param name="Session">The restored Session, or null when rejected.</param>
    /// <param name="Rejection">Why the snapshot was rejected, or None.</param>
    /// <param name="Detail">A sentence naming what was wrong, or null.</param>
    public sealed record SessionRestoreResult(
        bool Succeeded,
        SessionState Session,
        SessionRestoreRejection Rejection,
        string Detail
    )
    {
        /// <summary>
        /// Builds a successful result.
        /// </summary>
        /// <param name="session">The restored Session.</param>
        /// <returns>The result.</returns>
        public static SessionRestoreResult Restored(SessionState session)
        {
            return new SessionRestoreResult(true, session, SessionRestoreRejection.None, null);
        }

        /// <summary>
        /// Builds a rejected result carrying no Session.
        /// </summary>
        /// <param name="rejection">Why the snapshot was rejected.</param>
        /// <param name="detail">A sentence naming what was wrong.</param>
        /// <returns>The result.</returns>
        public static SessionRestoreResult Rejected(SessionRestoreRejection rejection, string detail)
        {
            return new SessionRestoreResult(false, null, rejection, detail);
        }
    }
}