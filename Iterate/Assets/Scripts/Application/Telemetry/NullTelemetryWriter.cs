namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// A telemetry writer that writes nothing, registered in release builds so the recorder's
    /// wiring is identical in every build.
    /// </summary>
    public sealed class NullTelemetryWriter : ITelemetryWriter
    {
        /// <summary>
        /// Does nothing.
        /// </summary>
        /// <param name="run">The run identity.</param>
        public void Open(string run) { }

        /// <summary>
        /// Does nothing.
        /// </summary>
        /// <param name="run">The run identity.</param>
        /// <param name="line">The encoded line.</param>
        public void Append(string run, string line) { }

        /// <summary>
        /// Does nothing.
        /// </summary>
        /// <param name="run">The run identity.</param>
        public void Close(string run) { }
    }
}