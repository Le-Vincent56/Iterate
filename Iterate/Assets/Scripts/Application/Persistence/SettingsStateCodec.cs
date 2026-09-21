using Iterate.Application.Content.Json;

namespace Iterate.Application.Persistence
{
    /// <summary>
    /// Reads and writes <see cref="SettingsState"/>. The shell has no members yet, so a payload carrying
    /// nothing but the envelope's own version member loads as the default — which is exactly what lets a
    /// later build add a preference without every settings file written today becoming unreadable.
    /// </summary>
    public static class SettingsStateCodec
    {
        /// <summary>
        /// Writes the settings as a payload.
        /// </summary>
        /// <param name="state">The settings to write.</param>
        /// <returns>The payload.</returns>
        public static JsonObject ToJson(SettingsState state)
        {
            return new JsonObjectBuilder().Build();
        }

        /// <summary>
        /// Reads the settings from a payload.
        /// </summary>
        /// <param name="payload">The payload to read.</param>
        /// <returns>The decoded settings.</returns>
        public static CodecResult<SettingsState> FromJson(JsonObject payload)
        {
            if (payload == null)
                return CodecResult<SettingsState>.Fail("settings: the payload is absent.");

            return CodecResult<SettingsState>.Ok(SettingsState.Default);
        }
    }
}