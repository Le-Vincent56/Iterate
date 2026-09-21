namespace Iterate.Application.Persistence
{
    /// <summary>
    /// The outcome of deleting a save file.
    /// </summary>
    /// <param name="Outcome">How the delete ended.</param>
    /// <param name="Defect">A sentence naming what went wrong, or null.</param>
    public sealed record SaveFileDeleteResult(SaveFileDeleteOutcome Outcome, string Defect)
    {
        /// <summary>
        /// Builds a result reporting the file was deleted.
        /// </summary>
        /// <returns>The result.</returns>
        public static SaveFileDeleteResult Deleted()
        {
            return new SaveFileDeleteResult(SaveFileDeleteOutcome.Deleted, null);
        }

        /// <summary>
        /// Builds a result reporting the file was not there.
        /// </summary>
        /// <returns>The result.</returns>
        public static SaveFileDeleteResult Absent()
        {
            return new SaveFileDeleteResult(SaveFileDeleteOutcome.Absent, null);
        }

        /// <summary>
        /// Builds a result reporting the delete failed.
        /// </summary>
        /// <param name="defect">What went wrong.</param>
        /// <returns>The result.</returns>
        public static SaveFileDeleteResult Failed(string defect)
        {
            return new SaveFileDeleteResult(SaveFileDeleteOutcome.Failed, defect);
        }
    }
}