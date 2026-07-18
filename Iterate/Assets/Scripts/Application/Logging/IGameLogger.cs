namespace Iterate.Application.Logging
{
    /// <summary>
    /// A category-bound logger. Obtain one from <see cref="IGameLoggerFactory"/> and log through
    /// the <see cref="GameLog"/> extension methods; the per-call gate is a single level compare.
    /// </summary>
    public interface IGameLogger
    {
        /// <summary>
        /// The category this logger is bound to.
        /// </summary>
        LogCategory Category { get; }

        /// <summary>
        /// Whether entries at the given level pass this logger's minimum-level gate.
        /// </summary>
        /// <param name="level">The level to test.</param>
        /// <returns>True when an entry at that level would be written.</returns>
        bool IsEnabled(LogLevel level);

        /// <summary>
        /// Writes the entry if its level passes the gate.
        /// </summary>
        /// <param name="entry">The entry to write.</param>
        void Log(in LogEntry entry);
    }
}