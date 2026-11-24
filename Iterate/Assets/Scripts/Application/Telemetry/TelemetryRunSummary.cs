using System;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// One run on disk, read from its first and last complete lines.
    /// </summary>
    /// <param name="Run">The run identity.</param>
    /// <param name="Started">When its first line was written.</param>
    /// <param name="Mode">Its Session mode, or null when it has no line.</param>
    /// <param name="Termination">How it ended.</param>
    public sealed record TelemetryRunSummary(
        string Run,
        DateTimeOffset Started,
        string Mode,
        TelemetryTermination Termination
    );
}