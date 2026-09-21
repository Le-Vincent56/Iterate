namespace Iterate.Application.Persistence
{
    /// <summary>
    /// The outcome of loading one save file. A value is present only when the file loaded; the version
    /// only when one could be read; the defect only when something went wrong.
    /// </summary>
    /// <typeparam name="T">The state type loaded.</typeparam>
    /// <param name="Outcome">How the load ended.</param>
    /// <param name="Value">The loaded state, or null.</param>
    /// <param name="FormatVersion">The version found, or zero.</param>
    /// <param name="Defect">A sentence naming what went wrong, or null.</param>
    public sealed record LoadResult<T>(
        LoadOutcome Outcome,
        T Value,
        int FormatVersion,
        string Defect
    )
        where T : class
    {
        /// <summary>
        /// The loaded state, or the given fallback for every other outcome. A first launch and an
        /// unreadable file both start the player from the same place; only the log tells them apart.
        /// </summary>
        /// <param name="fallback">The state to use when nothing was loaded.</param>
        /// <returns>The state to act on.</returns>
        public T ValueOr(T fallback) => Outcome == LoadOutcome.Loaded ? Value : fallback;
    }
}