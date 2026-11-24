namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// How a run ended, read from its last complete line.
    /// </summary>
    public enum TelemetryTermination
    {
        /// <summary>
        /// The Session was completed.
        /// </summary>
        Completed,

        /// <summary>
        /// The Session failed.
        /// </summary>
        Failed,

        /// <summary>
        /// The Session was abandoned.
        /// </summary>
        Abandoned,

        /// <summary>
        /// A command threw and the run ended as an unrecoverable error.
        /// </summary>
        Error,

        /// <summary>
        /// The run has no end line: it is still active, or it was interrupted by a quit or crash.
        /// </summary>
        ClosedWithoutEnd,
    }
}