using System;
using Iterate.Application.Telemetry;

namespace Iterate.Infrastructure.Telemetry
{
    /// <summary>
    /// Issues run identities from fresh GUIDs, as <c>run-</c> and 32 lowercase hexadecimal digits.
    /// </summary>
    public sealed class GuidTelemetryRunIdentitySource : ITelemetryRunIdentitySource
    {
        /// <summary>
        /// Issues the next run identity.
        /// </summary>
        /// <returns>The identity.</returns>
        public string Next()
        {
            return $"run-{Guid.NewGuid():N}";
        }
    }
}