namespace Iterate.Application.Persistence
{
    /// <summary>
    /// The names of the three files a Session's persistent state lives in, and the format version they
    /// are written at. The record codec carries its own version: a generated diagnostic artifact and a
    /// save file change shape for different reasons and must be able to move independently.
    /// </summary>
    public static class SaveFiles
    {
        /// <summary>
        /// The format version every save file is written at.
        /// </summary>
        public const int FormatVersion = 1;

        /// <summary>
        /// The settings file's name.
        /// </summary>
        public const string Settings = "settings.json";

        /// <summary>
        /// The meta file's name.
        /// </summary>
        public const string Meta = "meta.json";

        /// <summary>
        /// The onboarding checkpoint file's name.
        /// </summary>
        public const string Checkpoint = "checkpoint.json";
    }
}