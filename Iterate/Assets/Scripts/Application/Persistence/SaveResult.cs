namespace Iterate.Application.Persistence
{
    /// <summary>
    /// The outcome of saving one file.
    /// </summary>
    /// <param name="Outcome">How the save ended.</param>
    /// <param name="Defect">A sentence naming what went wrong, or null.</param>
    public sealed record SaveResult(SaveOutcome Outcome, string Defect)
    {
        /// <summary>
        /// Builds a result reporting the file was written.
        /// </summary>
        /// <returns>The result.</returns>
        public static SaveResult Saved()
        {
            return new SaveResult(SaveOutcome.Saved, null);
        }

        /// <summary>
        /// Builds a result reporting the save failed.
        /// </summary>
        /// <param name="defect">What went wrong.</param>
        /// <returns>The result.</returns>
        public static SaveResult Failed(string defect)
        {
            return new SaveResult(SaveOutcome.Failed, defect);
        }
    }
}