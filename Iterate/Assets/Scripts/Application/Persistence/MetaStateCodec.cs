using Iterate.Application.Content.Json;

namespace Iterate.Application.Persistence
{
    /// <summary>
    /// Reads and writes <see cref="MetaState"/>. An absent flag is a defect rather than false: defaulting
    /// it would silently replay the onboarding for a player who had already finished it, which is the
    /// difference between missing data and a valid value that this codec exists to keep.
    /// </summary>
    public static class MetaStateCodec
    {
        private const string OnboardingCompleteMember = "onboardingComplete";

        /// <summary>
        /// Writes the meta state as a payload.
        /// </summary>
        /// <param name="state">The state to write.</param>
        /// <returns>The payload.</returns>
        public static JsonObject ToJson(MetaState state)
        {
            return new JsonObjectBuilder()
                .Add(OnboardingCompleteMember, state.OnboardingComplete)
                .Build();
        }

        /// <summary>
        /// Reads the meta state from a payload.
        /// </summary>
        /// <param name="payload">The payload to read.</param>
        /// <returns>The decoded state, or a defect naming the member that was wrong.</returns>
        public static CodecResult<MetaState> FromJson(JsonObject payload)
        {
            if (payload == null)
                return CodecResult<MetaState>.Fail("meta: the payload is absent.");

            JsonMemberReader reader = new(payload, "meta");
            bool complete = reader.Bool(OnboardingCompleteMember);
            if (reader.Failed)
                return CodecResult<MetaState>.Fail(reader.Defect);

            return CodecResult<MetaState>.Ok(new MetaState(complete));
        }
    }
}