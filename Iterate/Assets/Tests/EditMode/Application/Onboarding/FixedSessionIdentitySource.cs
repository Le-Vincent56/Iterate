using Iterate.Application.Onboarding;

namespace Iterate.Application.Onboarding.Tests
{
    /// <summary>
    /// An identity source that numbers its identities, so a suite can predict them.
    /// </summary>
    public sealed class FixedSessionIdentitySource : ISessionIdentitySource
    {
        private int _issued;

        /// <summary>
        /// How many identity pairs have been issued.
        /// </summary>
        public int Issued => _issued;

        /// <summary>
        /// Issues the next numbered identity pair.
        /// </summary>
        /// <returns>The pair.</returns>
        public SessionIdentities Next()
        {
            _issued++;
            return new SessionIdentities($"ordinary-{_issued}", $"ordinary-seed-{_issued}");
        }
    }
}
