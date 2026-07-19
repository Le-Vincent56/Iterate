using System.Collections.Generic;
using Iterate.Application.Logging;

namespace Iterate.Composition.Root.Tests
{
    /// <summary>
    /// An in-memory <see cref="ILogSink"/> for boot tests: records each entry's level and message so
    /// a test can assert what was logged without going through the Unity console.
    /// </summary>
    public sealed class CapturingLogSink : ILogSink
    {
        private readonly List<(LogLevel Level, string Message)> _entries = new();

        /// <summary>
        /// The captured entries in write order.
        /// </summary>
        public IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries;

        /// <summary>
        /// Records the entry's level and message.
        /// </summary>
        /// <param name="entry">The entry being written.</param>
        public void Write(in LogEntry entry)
        {
            _entries.Add((entry.Level, entry.Message));
        }

        /// <summary>
        /// Whether an entry at the given level with the given message was captured.
        /// </summary>
        /// <param name="level">The level to match.</param>
        /// <param name="message">The message to match.</param>
        /// <returns>True when a matching entry is present.</returns>
        public bool Has(LogLevel level, string message)
        {
            foreach ((LogLevel Level, string Message) entry in _entries)
            {
                if (entry.Level == level && entry.Message == message)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether any Error-level entry was captured.
        /// </summary>
        /// <returns>True when at least one Error was written.</returns>
        public bool HasError()
        {
            foreach ((LogLevel Level, string Message) entry in _entries)
            {
                if (entry.Level == LogLevel.Error)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
