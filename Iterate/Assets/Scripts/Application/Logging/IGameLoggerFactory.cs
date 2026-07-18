namespace Iterate.Application.Logging
{
    /// <summary>
    /// Creates category-bound loggers. Inject once and call
    /// <see cref="Create"/> in a constructor or post-injection setup step.
    /// </summary>
    public interface IGameLoggerFactory
    {
        /// <summary>
        /// Creates a logger bound to the given category, with its minimum level resolved once.
        /// </summary>
        /// <param name="category">The category to bind.</param>
        /// <returns>The bound logger.</returns>
        IGameLogger Create(in LogCategory category);
    }
}