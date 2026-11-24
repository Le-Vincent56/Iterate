using System;
using Iterate.Application.Onboarding;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// Decorates the lifecycle router for telemetry. Each call reaches the router first and then
    /// raises one event, so a subscriber reading the onboarding failure count sees it already
    /// incremented. The router's disposition is returned unchanged.
    /// </summary>
    public sealed class TelemetryLifecycleTap : ISessionLifecycle
    {
        private readonly SessionLifecycleRouter _inner;

        /// <summary>
        /// Raised after the router has handled a Process entry.
        /// </summary>
        public event Action<ProcessEntry> ProcessEntered;

        /// <summary>
        /// Raised after the router has answered a Process failure.
        /// </summary>
        public event Action<ProcessFailure> ProcessFailed;

        /// <summary>
        /// Raised after the router has handled a Session completion.
        /// </summary>
        public event Action SessionCompleted;

        /// <summary>
        /// Raised after the router has handled a Session abandonment.
        /// </summary>
        public event Action SessionAbandoned;

        /// <summary>
        /// The router this tap decorates.
        /// </summary>
        public SessionLifecycleRouter Inner => _inner;

        /// <summary>
        /// Creates the tap over the router.
        /// </summary>
        /// <param name="inner">The lifecycle router.</param>
        /// <exception cref="ArgumentNullException">Thrown when the router is null.</exception>
        public TelemetryLifecycleTap(SessionLifecycleRouter inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <summary>
        /// Forwards a Process entry to the router, then raises <see cref="ProcessEntered"/>.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <param name="process">The Process entered.</param>
        /// <param name="route">The committed route, or null.</param>
        public void OnProcessEntry(SessionState session, ProcessID process, RouteID? route)
        {
            _inner.OnProcessEntry(session, process, route);
            ProcessEntered?.Invoke(new ProcessEntry(process, route));
        }

        /// <summary>
        /// Forwards a Process failure to the router, then raises <see cref="ProcessFailed"/>.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <param name="process">The Process that failed.</param>
        /// <param name="route">The committed route, or null.</param>
        /// <returns>The router's disposition, unchanged.</returns>
        public ProcessFailureDisposition OnProcessFailed(SessionState session, ProcessID process, RouteID? route)
        {
            ProcessFailureDisposition disposition = _inner.OnProcessFailed(session, process, route);
            ProcessFailed?.Invoke(new ProcessFailure(process, route, disposition));
            return disposition;
        }

        /// <summary>
        /// Forwards a Session completion to the router, then raises <see cref="SessionCompleted"/>.
        /// </summary>
        /// <param name="session">The Session.</param>
        public void OnSessionCompleted(SessionState session)
        {
            _inner.OnSessionCompleted(session);
            SessionCompleted?.Invoke();
        }

        /// <summary>
        /// Forwards a Session abandonment to the router, then raises <see cref="SessionAbandoned"/>.
        /// </summary>
        /// <param name="session">The Session.</param>
        public void OnSessionAbandoned(SessionState session)
        {
            _inner.OnSessionAbandoned(session);
            SessionAbandoned?.Invoke();
        }
    }
}