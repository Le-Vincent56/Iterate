using System.Collections.Generic;
using Iterate.Application.Logging;

namespace Iterate.Infrastructure.Telemetry.Tests
{
    /// <summary>
    /// A log sink that keeps every entry, for the Infrastructure suites, which cannot see the
    /// Application tests' fake.
    /// </summary>
    public sealed class RecordingLogSink : ILogSink
    {
        /// <summary>
        /// Every entry written, in order.
        /// </summary>
        public List<LogEntry> Entries { get; } = new();

        /// <summary>
        /// Keeps an entry.
        /// </summary>
        /// <param name="entry">The entry.</param>
        public void Write(in LogEntry entry)
        {
            Entries.Add(entry);
        }

        /// <summary>
        /// Counts the entries at a level.
        /// </summary>
        /// <param name="level">The level.</param>
        /// <returns>The count.</returns>
        public int CountAt(LogLevel level)
        {
            int count = 0;
            foreach (LogEntry entry in Entries)
            {
                if (entry.Level == level)
                    count++;
            }

            return count;
        }
    }
}
