namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// Supplies a fresh identity for each telemetry run. It is separate from the Session identities,
    /// because every onboarding Session shares one fixed Session identity.
    /// </summary>
    public interface ITelemetryRunIdentitySource
    {
        /// <summary>
        /// Generates the identity for the next run.
        /// </summary>
        /// <returns>A run identity unused before.</returns>
        string Next();
    }
}