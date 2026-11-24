namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// The player's resources before and after one command.
    /// </summary>
    /// <param name="Before">The resources before, or null outside a Session.</param>
    /// <param name="After">The resources after, or null outside a Session.</param>
    public readonly record struct ResourceChange(TelemetryResources Before, TelemetryResources After);
}