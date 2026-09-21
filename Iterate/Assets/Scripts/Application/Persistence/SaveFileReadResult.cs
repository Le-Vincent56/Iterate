namespace Iterate.Application.Persistence
{
    /// <summary>
    /// The outcome of reading a save file. Text is present only when the file was found; a defect only
    /// when it could not be read.
    /// </summary>
    /// <param name="Outcome">How the read ended.</param>
    /// <param name="Text">The file's text, or null.</param>
    /// <param name="Defect">A sentence naming what went wrong, or null.</param>
    public sealed record SaveFileReadResult(SaveFileReadOutcome Outcome, string Text, string Defect)
    {
        /// <summary>
        /// Builds a result carrying the file's text.
        /// </summary>
        /// <param name="text">The file's text.</param>
        /// <returns>The result.</returns>
        public static SaveFileReadResult Found(string text)
        {
            return new SaveFileReadResult(SaveFileReadOutcome.Found, text, null);
        }

        /// <summary>
        /// Builds a result reporting the file does not exist.
        /// </summary>
        /// <returns>The result.</returns>
        public static SaveFileReadResult Absent()
        {
            return new SaveFileReadResult(SaveFileReadOutcome.Absent, null, null);
        }

        /// <summary>
        /// Builds a result reporting the file could not be read.
        /// </summary>
        /// <param name="defect">What went wrong.</param>
        /// <returns>The result.</returns>
        public static SaveFileReadResult Unreadable(string defect)
        {
            return new SaveFileReadResult(SaveFileReadOutcome.Unreadable, null, defect);
        }
    }
}