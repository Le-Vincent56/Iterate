using System;
using Iterate.Application.Onboarding;

namespace Iterate.Infrastructure.Identity
{
    /// <summary>
    /// Issues ordinary Session identities from fresh GUIDs, so no two Sessions share an identity or a
    /// seed and neither reveals anything about the player.
    /// </summary>
    public sealed class GuidSessionIdentitySource : ISessionIdentitySource
    {
        /// <summary>
        /// Issues a Session identity and a seed identity, each from its own fresh GUID.
        /// </summary>
        /// <returns>The pair.</returns>
        public SessionIdentities Next()
        {
            return new SessionIdentities($"session-{Guid.NewGuid():N}", $"seed-{Guid.NewGuid():N}");
        }
    }
}