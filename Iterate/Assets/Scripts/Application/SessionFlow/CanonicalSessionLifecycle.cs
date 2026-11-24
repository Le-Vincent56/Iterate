using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The lifecycle with no checkpoints: every moment is ignored and a failed Process ends the
    /// Session.
    /// </summary>
    public sealed class CanonicalSessionLifecycle : ISessionLifecycle
    {
        /// <summary>
        /// Ignores Process entry.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <param name="process">The Process being entered.</param>
        /// <param name="route">The committed route, or null.</param>
        public void OnProcessEntry(SessionState session, ProcessID process, RouteID? route) { }

        /// <summary>
        /// Ends the Session on any failure.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <param name="process">The failed Process.</param>
        /// <param name="route">The committed route, or null.</param>
        /// <returns>Always <see cref="ProcessFailureDisposition.EndSession"/>.</returns>
        public ProcessFailureDisposition OnProcessFailed(SessionState session, ProcessID process, RouteID? route)
        {
            return ProcessFailureDisposition.EndSession;
        }

        /// <summary>
        /// Ignores completion.
        /// </summary>
        /// <param name="session">The Session.</param>
        public void OnSessionCompleted(SessionState session) { }

        /// <summary>
        /// Ignores abandonment.
        /// </summary>
        /// <param name="session">The Session.</param>
        public void OnSessionAbandoned(SessionState session) { }
    }
}