namespace Iterate.Application.Persistence
{
    /// <summary>
    /// How deleting a save file ended. Deleting a file that is already gone is a success from the
    /// caller's point of view but is reported distinctly, so a caller that cares can tell.
    /// </summary>
    public enum SaveFileDeleteOutcome
    {
        /// <summary>
        /// The file existed and was deleted.
        /// </summary>
        Deleted,

        /// <summary>
        /// The file did not exist.
        /// </summary>
        Absent,

        /// <summary>
        /// The file exists and could not be deleted.
        /// </summary>
        Failed
    }
}