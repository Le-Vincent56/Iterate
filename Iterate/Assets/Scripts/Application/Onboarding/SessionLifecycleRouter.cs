using System;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Onboarding
{
    /// <summary>
    /// The lifecycle Session Flow is given. It forwards every moment to the onboarding lifecycle or to
    /// the canonical one, by the mode the launch service sets before each Session begins. A Session
    /// begun any other way runs as an ordinary Session.
    /// </summary>
    public sealed class SessionLifecycleRouter : ISessionLifecycle
    {
        private readonly OnboardingLifecycle _onboarding;
        private readonly CanonicalSessionLifecycle _ordinary;

        /// <summary>
        /// Which lifecycle the next calls go to; Ordinary until set.
        /// </summary>
        public SessionMode Mode { get; set; } = SessionMode.Ordinary;

        /// <summary>
        /// Creates the router over the two lifecycles.
        /// </summary>
        /// <param name="onboarding">The onboarding lifecycle.</param>
        /// <param name="ordinary">The canonical lifecycle for ordinary Sessions.</param>
        /// <exception cref="ArgumentNullException">Thrown when either lifecycle is null.</exception>
        public SessionLifecycleRouter(OnboardingLifecycle onboarding, CanonicalSessionLifecycle ordinary)
        {
            _onboarding = onboarding ?? throw new ArgumentNullException(nameof(onboarding));
            _ordinary = ordinary ?? throw new ArgumentNullException(nameof(ordinary));
        }

        /// <summary>
        /// Forwards a Process entry.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <param name="process">The Process being entered.</param>
        /// <param name="route">The committed route, or null.</param>
        public void OnProcessEntry(SessionState session, ProcessID process, RouteID? route)
        {
            Current().OnProcessEntry(session, process, route);
        }

        /// <summary>
        /// Forwards a Process failure.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <param name="process">The failed Process.</param>
        /// <param name="route">The committed route, or null.</param>
        /// <returns>The selected lifecycle's disposition.</returns>
        public ProcessFailureDisposition OnProcessFailed(SessionState session, ProcessID process, RouteID? route)
        {
            return Current().OnProcessFailed(session, process, route);
        }

        /// <summary>
        /// Forwards completion.
        /// </summary>
        /// <param name="session">The Session.</param>
        public void OnSessionCompleted(SessionState session)
        {
            Current().OnSessionCompleted(session);
        }

        /// <summary>
        /// Forwards abandonment.
        /// </summary>
        /// <param name="session">The Session.</param>
        public void OnSessionAbandoned(SessionState session)
        {
            Current().OnSessionAbandoned(session);
        }

        /// <summary>
        /// The lifecycle the current mode selects.
        /// </summary>
        /// <returns>The lifecycle.</returns>
        private ISessionLifecycle Current()
        {
            return Mode == SessionMode.Onboarding ? _onboarding : _ordinary;
        }
    }
}