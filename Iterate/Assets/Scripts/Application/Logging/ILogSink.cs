namespace Iterate.Application.Logging
{
    /// <summary>
    /// The outbound port of the logging seam. Exactly one implementation is registered; entries
    /// that pass a logger's level gate are written here.
    /// </summary>
    public interface ILogSink
    {
        /// <summary>
        /// Writes one entry to the sink's destination.
        /// </summary>
        /// <param name="entry">The entry to write.</param>
        void Write(in LogEntry entry);
    }
}