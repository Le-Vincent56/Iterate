namespace Iterate.Application.Persistence
{
    /// <summary>
    /// The player's settings. A versioned shell today: the preference fields arrive with the playback
    /// and interface work, and the shape exists now so that a settings file written by this build stays
    /// readable when they do.
    /// </summary>
    public sealed record SettingsState
    {
        /// <summary>
        /// The settings a player who has changed nothing has.
        /// </summary>
        public static SettingsState Default { get; } = new();
    }
}