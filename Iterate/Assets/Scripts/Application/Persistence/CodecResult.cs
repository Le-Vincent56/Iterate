namespace Iterate.Application.Persistence
{
    /// <summary>
    /// The outcome of decoding a state class from a JSON payload. A defective payload yields no value at
    /// all rather than a partly-filled one, and the defect names the member that was wrong — a missing
    /// required member is never a default zero.
    /// </summary>
    /// <typeparam name="T">The state type decoded.</typeparam>
    /// <param name="Succeeded">Whether the payload decoded.</param>
    /// <param name="Value">The decoded state, or null.</param>
    /// <param name="Defect">A sentence naming the member that was wrong, or null.</param>
    public sealed record CodecResult<T>(bool Succeeded, T Value, string Defect) where T : class
    {
        /// <summary>
        /// Builds a successful result.
        /// </summary>
        /// <param name="value">The decoded state.</param>
        /// <returns>The result.</returns>
        public static CodecResult<T> Ok(T value)
        {
            return new CodecResult<T>(true, value, null);
        }

        /// <summary>
        /// Builds a defective result carrying no value.
        /// </summary>
        /// <param name="defect">The member that was wrong and why.</param>
        /// <returns>The result.</returns>
        public static CodecResult<T> Fail(string defect)
        {
            return new CodecResult<T>(false, null, defect);
        }
    }
}