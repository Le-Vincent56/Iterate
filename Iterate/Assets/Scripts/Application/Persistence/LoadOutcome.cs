namespace Iterate.Application.Persistence
{
    /// <summary>
    /// How loading a save file ended. A file that is simply not there is an ordinary outcome, not a
    /// failure: a first-time player has none of the three.
    /// </summary>
    public enum LoadOutcome
    {
        /// <summary>
        /// The file was read and decoded.
        /// </summary>
        Loaded,

        /// <summary>
        /// The file does not exist.
        /// </summary>
        Absent,

        /// <summary>
        /// The file exists but could not be read or decoded.
        /// </summary>
        Unreadable,

        /// <summary>
        /// The file was written at a format version this build does not read.
        /// </summary>
        UnsupportedVersion
    }
}