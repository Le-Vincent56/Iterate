using Iterate.Application.Telemetry;

namespace Iterate.Application.Telemetry.Tests
{
    /// <summary>
    /// Issues run identities <c>run-1</c>, <c>run-2</c> and so on.
    /// </summary>
    public sealed class FixedTelemetryRunIdentitySource : ITelemetryRunIdentitySource
    {
        private int _issued;

        /// <summary>
        /// Issues the next run identity.
        /// </summary>
        /// <returns>The identity.</returns>
        public string Next()
        {
            _issued++;
            return $"run-{_issued}";
        }
    }
}
