using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The Session's lifecycle moments, offered to whatever owns checkpoints and failure handling.
    /// Every call is synchronous and happens while the Session is consistent; an implementation that
    /// needs input or output owns how it waits for it.
    /// </summary>
    public interface ISessionLifecycle
    {
        /// <summary>
        /// Called on entering a Process, after any Branch confirmation and before the Process is
        /// created, so nothing the Process allocates has happened yet.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <param name="process">The Process being entered.</param>
        /// <param name="route">The committed route, or null before one is committed.</param>
        void OnProcessEntry(SessionState session, ProcessID process, RouteID? route);

        /// <summary>
        /// Called when a Process fails; the answer decides what the Session does next.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <param name="process">The failed Process.</param>
        /// <param name="route">The committed route, or null before one is committed.</param>
        /// <returns>What the Session does next.</returns>
        ProcessFailureDisposition OnProcessFailed(SessionState session, ProcessID process, RouteID? route);

        /// <summary>
        /// Called when the Session completes its System.
        /// </summary>
        /// <param name="session">The Session.</param>
        void OnSessionCompleted(SessionState session);

        /// <summary>
        /// Called when the player abandons the Session.
        /// </summary>
        /// <param name="session">The Session.</param>
        void OnSessionAbandoned(SessionState session);
    }
}