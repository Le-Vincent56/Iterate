namespace Iterate.Application.Persistence
{
    /// <summary>
    /// How reading a save file ended. A file that is simply not there is an ordinary outcome, not a
    /// failure: a first-time player has none.
    /// </summary>
    public enum SaveFileReadOutcome
    {
        /// <summary>
        /// The file was read and its text returned.
        /// </summary>
        Found,

        /// <summary>
        /// The file does not exist.
        /// </summary>
        Absent,

        /// <summary>
        /// The file exists but could not be read.
        /// </summary>
        Unreadable
    }
}