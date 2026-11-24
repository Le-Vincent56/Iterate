namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// The player's resources after a telemetry event: the open Process's Bytes, the Session's
    /// Tokens, and RAM in use against its capacity.
    /// </summary>
    /// <param name="Bytes">The open Process's Bytes, or zero when no Process is open.</param>
    /// <param name="Tokens">The Session's Tokens.</param>
    /// <param name="RAMUsed">The RAM the installed Dependencies use.</param>
    /// <param name="RAMCapacity">The RAM capacity.</param>
    public sealed record TelemetryResources(
        int Bytes,
        int Tokens,
        int RAMUsed,
        int RAMCapacity
    );
}