using System;

namespace Iterate.Application.Logging
{
    /// <summary>
    /// The concrete logger factory: holds the single sink and the filter configuration, and
    /// binds each created logger's minimum level at creation time.
    /// </summary>
    public sealed class GameLoggerFactory : IGameLoggerFactory
    {
        /// <summary>
        /// The concrete category-bound logger; its per-call gate is one integer compare against
        /// the minimum level resolved at creation.
        /// </summary>
        private sealed class GameLogger : IGameLogger
        {
            private readonly LogLevel _minimumLevel;

            private readonly ILogSink _sink;

            /// <summary>
            /// The category this logger is bound to.
            /// </summary>
            public LogCategory Category { get; }
            
            public GameLogger(LogCategory category, LogLevel minimumLevel, ILogSink sink)
            {
                Category = category;
                _minimumLevel = minimumLevel;
                _sink = sink;
            }

            /// <summary>
            /// Whether entries at the given level pass the creation-time minimum.
            /// </summary>
            /// <param name="level">The level to test.</param>
            /// <returns>True when an entry at that level would be written.</returns>
            public bool IsEnabled(LogLevel level) => level >= _minimumLevel;

            /// <summary>
            /// Writes the entry to the sink when its level passes the gate.
            /// </summary>
            /// <param name="entry">The entry to write.</param>
            public void Log(in LogEntry entry)
            {
                if (!IsEnabled(entry.Level))
                {
                    return;
                }

                _sink.Write(in entry);
            }
        }

        private readonly ILogSink _sink;

        private readonly LogFilterConfig _filterConfig;

        public GameLoggerFactory(ILogSink sink, LogFilterConfig filterConfig)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _filterConfig = filterConfig ?? throw new ArgumentNullException(nameof(filterConfig));
        }

        /// <summary>
        /// Creates a logger bound to the given category, resolving its minimum level once.
        /// </summary>
        /// <param name="category">The category to bind; must carry a non-empty name.</param>
        /// <returns>The bound logger.</returns>
        /// <exception cref="ArgumentException">Thrown when the category has a null or empty name.</exception>
        public IGameLogger Create(in LogCategory category)
        {
            if (string.IsNullOrEmpty(category.Name))
            {
                throw new ArgumentException("A logger category requires a non-empty name.", nameof(category));
            }

            return new GameLogger(category, _filterConfig.MinimumLevelFor(in category), _sink);
        }
    }
}