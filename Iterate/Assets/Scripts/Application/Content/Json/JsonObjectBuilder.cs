using System;
using System.Collections.Generic;

namespace Iterate.Application.Content.Json
{
    /// <summary>
    /// Accumulates JSON object members in insertion order and builds an immutable
    /// <see cref="JsonObject"/> from them. Exists because the object carries its key order beside its
    /// member dictionary, and because a codec should not have to maintain the two in step by hand.
    /// </summary>
    public sealed class JsonObjectBuilder
    {
        private readonly List<string> _keys = new();
        private readonly Dictionary<string, JsonValue> _members = new(StringComparer.Ordinal);

        /// <summary>
        /// Adds a member.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <param name="value">The member value.</param>
        /// <returns>This builder, for chaining.</returns>
        /// <exception cref="ArgumentException">Thrown on a null or empty key, a null value, or a duplicate key.</exception>
        public JsonObjectBuilder Add(string key, JsonValue value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("a JSON member key cannot be null or empty.", nameof(key));

            if (value == null)
                throw new ArgumentException("a JSON member value cannot be null; use JsonValues.Null().", nameof(value));

            if (_members.ContainsKey(key))
                throw new ArgumentException($"duplicate JSON member key '{key}'.", nameof(key));

            _keys.Add(key);
            _members[key] = value;
            return this;
        }

        /// <summary>
        /// Adds a string member, writing a null value as a JSON null literal.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <param name="value">The string value, or null.</param>
        /// <returns>This builder, for chaining.</returns>
        public JsonObjectBuilder Add(string key, string value) => Add(key, JsonValues.StringOrNull(value));

        /// <summary>
        /// Adds an integer member.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <param name="value">The integer value.</param>
        /// <returns>This builder, for chaining.</returns>
        public JsonObjectBuilder Add(string key, long value) => Add(key, JsonValues.Integer(value));

        /// <summary>
        /// Adds a boolean member.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <param name="value">The boolean value.</param>
        /// <returns>This builder, for chaining.</returns>
        public JsonObjectBuilder Add(string key, bool value) => Add(key, JsonValues.Bool(value));

        /// <summary>
        /// Adds a member whose value is a JSON null literal.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <returns>This builder, for chaining.</returns>
        public JsonObjectBuilder AddNull(string key) => Add(key, JsonValues.Null());

        /// <summary>
        /// Builds the object from the members added so far. The result copies them, so the builder may
        /// keep being used without changing an object it has already produced.
        /// </summary>
        /// <returns>The built object, at line and column zero.</returns>
        public JsonObject Build()
        {
            List<string> keys = new(_keys);
            Dictionary<string, JsonValue> members = new(_members, StringComparer.Ordinal);
            return new JsonObject(0, 0, keys, members);
        }
    }
}