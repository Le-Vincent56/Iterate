namespace Iterate.Application.Persistence
{
    /// <summary>
    /// How saving a file ended.
    /// </summary>
    public enum SaveOutcome
    {
        /// <summary>
        /// The file was written.
        /// </summary>
        Saved,

        /// <summary>
        /// The file could not be written.
        /// </summary>
        Failed
    }
}