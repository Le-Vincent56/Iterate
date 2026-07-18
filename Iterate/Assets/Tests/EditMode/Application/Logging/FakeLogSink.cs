using System.Collections.Generic;
using Iterate.Application.Logging;

namespace Iterate.Application.Logging.Tests
{
    /// <summary>
    /// Test double for <see cref="ILogSink"/> that records every written entry for assertion.
    /// </summary>
    public sealed class FakeLogSink : ILogSink
    {
        /// <summary>
        /// Every entry written to this sink, in write order.
        /// </summary>
        public List<LogEntry> Entries { get; } = new();

        /// <summary>
        /// Records the entry.
        /// </summary>
        /// <param name="entry">The entry to record.</param>
        public void Write(in LogEntry entry)
        {
            Entries.Add(entry);
        }
    }
}
