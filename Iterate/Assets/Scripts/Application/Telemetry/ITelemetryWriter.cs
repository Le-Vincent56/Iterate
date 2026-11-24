namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// Appends telemetry lines to a run's file, one flushed line at a time. Implementations never
    /// throw: an I/O failure is logged once for the run, and that run's later lines are dropped.
    /// </summary>
    public interface ITelemetryWriter
    {
        /// <summary>
        /// Creates the run's file.
        /// </summary>
        /// <param name="run">The run identity.</param>
        void Open(string run);

        /// <summary>
        /// Appends one encoded line to the run's file and flushes it.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <param name="line">The encoded line, ending in a newline.</param>
        void Append(string run, string line);

        /// <summary>
        /// Releases the run's file.
        /// </summary>
        /// <param name="run">The run identity.</param>
        void Close(string run);
    }
}