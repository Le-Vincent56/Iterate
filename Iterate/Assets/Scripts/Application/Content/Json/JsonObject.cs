using System.Collections.Generic;

namespace Iterate.Application.Content.Json
{
    /// <summary>
    /// A JSON object: its keys in document order plus by-key lookup. Duplicate keys are rejected by
    /// the reader, so each key is unique.
    /// </summary>
    public sealed record JsonObject : JsonValue
    {
        private readonly IReadOnlyDictionary<string, JsonValue> _members;

        /// <summary>
        /// The object's keys in document order.
        /// </summary>
        public IReadOnlyList<string> Keys { get; }

        public JsonObject(
            int line,
            int column,
            IReadOnlyList<string> keys,
            IReadOnlyDictionary<string, JsonValue> members
        ) : base(line, column)
        {
            Keys = keys;
            _members = members;
        }

        /// <summary>
        /// Looks up a member value by key.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <param name="value">The found value, or null when the key is absent.</param>
        /// <returns>True when the key resolves to a member.</returns>
        public bool TryGet(string key, out JsonValue value) => _members.TryGetValue(key, out value);
    }
}