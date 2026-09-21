namespace Iterate.Application.Persistence
{
    /// <summary>
    /// The outcome of writing a save file. A write either happened or did not; there is no third state,
    /// because the adapter replaces the target only once the new text is safely on disk.
    /// </summary>
    /// <param name="Succeeded">Whether the file was written.</param>
    /// <param name="Defect">A sentence naming what went wrong, or null.</param>
    public sealed record SaveFileWriteResult(bool Succeeded, string Defect)
    {
        /// <summary>
        /// Builds a result reporting the file was written.
        /// </summary>
        /// <returns>The result.</returns>
        public static SaveFileWriteResult Written()
        {
            return new SaveFileWriteResult(true, null);
        }

        /// <summary>
        /// Builds a result reporting the write failed.
        /// </summary>
        /// <param name="defect">What went wrong.</param>
        /// <returns>The result.</returns>
        public static SaveFileWriteResult Failed(string defect)
        {
            return new SaveFileWriteResult(false, defect);
        }
    }
}