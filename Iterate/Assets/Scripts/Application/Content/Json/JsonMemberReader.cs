using System;
using System.Collections.Generic;

namespace Iterate.Application.Content.Json
{
    /// <summary>
    /// Reads the members of a JSON object for a codec, failing fast: a missing or wrong-kinded member
    /// records a defect naming its path and stops the reader, and every accessor afterwards returns a
    /// default without recording anything further, so the first defect is the one reported. A missing
    /// member is never a default zero, which keeps missing data distinguishable from a valid zero.
    /// A reader made by <see cref="Child"/> shares its parent's defect, so a defect recorded deep in a
    /// sub-graph surfaces on the reader the entry-point codec checks.
    /// </summary>
    public sealed class JsonMemberReader
    {
        private static readonly JsonObject _absentObject = new(
            0,
            0,
            new List<string>(),
            new Dictionary<string, JsonValue>(StringComparer.Ordinal)
        );

        private readonly JsonObject _source;

        private readonly string _path;

        private readonly DefectSink _defects;

        /// <summary>
        /// Whether a defect has been recorded, on this reader or on one of its children.
        /// </summary>
        public bool Failed => _defects.Defect != null;

        /// <summary>
        /// The first defect recorded, as "path.key: reason", or null while none has been.
        /// </summary>
        public string Defect => _defects.Defect;

        public JsonMemberReader(JsonObject source, string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("a member reader needs a path to name its defects by.", nameof(path));

            _source = source ?? throw new ArgumentException("a member reader needs an object to read.", nameof(source));
            _path = path;
            _defects = new DefectSink();
        }

        private JsonMemberReader(JsonObject source, string path, DefectSink defects)
        {
            _source = source;
            _path = path;
            _defects = defects;
        }

        /// <summary>
        /// Reads a required string member.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <returns>The string, or null when the member is absent, null, or not a string.</returns>
        public string String(string key)
        {
            if (!TryMember(key, out JsonValue member))
                return null;

            if (member is JsonString text)
                return text.Value;

            Record(key, "expected a string.");
            return null;
        }

        /// <summary>
        /// Reads a string member that may be written as a JSON null. The member must still be present:
        /// an absent key is a defect, a null value is not.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <returns>The string, or null when the member is a JSON null or a defect was recorded.</returns>
        public string OptionalString(string key)
        {
            if (!TryMember(key, out JsonValue member))
                return null;

            if (member is JsonNull _)
                return null;

            if (member is JsonString text)
                return text.Value;

            Record(key, "expected a string.");
            return null;
        }

        /// <summary>
        /// Reads a required integer member that fits a 32-bit integer.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <returns>The integer, or zero when a defect was recorded.</returns>
        public int Int(string key)
        {
            if (!TryMember(key, out JsonValue member))
                return 0;

            if (member is not JsonNumber number || !number.IsInteger)
            {
                Record(key, "expected an integer.");
                return 0;
            }

            if (number.IntegerValue < int.MinValue || number.IntegerValue > int.MaxValue)
            {
                Record(key, "the integer is outside the range of System.Int32.");
                return 0;
            }

            return (int)number.IntegerValue;
        }

        /// <summary>
        /// Reads an integer member that may be written as a JSON null. The member must still be present.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <returns>The integer, or null when the member is a JSON null or a defect was recorded.</returns>
        public int? OptionalInt(string key)
        {
            if (!TryMember(key, out JsonValue member))
                return null;

            if (member is JsonNull _)
                return null;

            int value = Int(key);
            return Failed ? (int?)null : value;
        }

        /// <summary>
        /// Reads a required integer member across the full 64-bit range.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <returns>The integer, or zero when a defect was recorded.</returns>
        public long Long(string key)
        {
            if (!TryMember(key, out JsonValue member))
                return 0L;

            if (member is not JsonNumber number || !number.IsInteger)
            {
                Record(key, "expected an integer.");
                return 0L;
            }

            return number.IntegerValue;
        }

        /// <summary>
        /// Reads a required boolean member.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <returns>The boolean, or false when a defect was recorded.</returns>
        public bool Bool(string key)
        {
            if (!TryMember(key, out JsonValue member))
                return false;

            if (member is JsonBool flag)
                return flag.Value;

            Record(key, "expected a boolean.");
            return false;
        }

        /// <summary>
        /// Reads a required array member.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <returns>The array, or null when a defect was recorded.</returns>
        public JsonArray Array(string key)
        {
            if (!TryMember(key, out JsonValue member))
                return null;

            if (member is JsonArray items)
                return items;

            Record(key, "expected an array.");
            return null;
        }

        /// <summary>
        /// Reads an array member that may be written as a JSON null. The member must still be present.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <returns>The array, or null when the member is a JSON null or a defect was recorded.</returns>
        public JsonArray OptionalArray(string key)
        {
            if (!TryMember(key, out JsonValue member))
                return null;

            if (member is JsonNull _)
                return null;

            if (member is JsonArray items)
                return items;

            Record(key, "expected an array.");
            return null;
        }

        /// <summary>
        /// Reads a required object member.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <returns>The object, or null when a defect was recorded.</returns>
        public JsonObject Object(string key)
        {
            if (!TryMember(key, out JsonValue member))
                return null;

            if (member is JsonObject nested)
                return nested;

            Record(key, "expected an object.");
            return null;
        }

        /// <summary>
        /// Reads an object member that may be written as a JSON null. The member must still be present.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <returns>The object, or null when the member is a JSON null or a defect was recorded.</returns>
        public JsonObject OptionalObject(string key)
        {
            if (!TryMember(key, out JsonValue member))
                return null;

            if (member is JsonNull _)
                return null;

            if (member is JsonObject nested)
                return nested;

            Record(key, "expected an object.");
            return null;
        }

        /// <summary>
        /// Reads a required object member as a reader over it, with the path extended by the key. The
        /// child shares this reader's defect, so a codec need only check the reader it started from.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <returns>A reader over the member, never null.</returns>
        public JsonMemberReader Child(string key)
        {
            JsonObject nested = Object(key);
            return new JsonMemberReader(nested ?? _absentObject, _path + "." + key, _defects);
        }

        /// <summary>
        /// Reads a required member naming an enumeration member. The name is matched exactly, so a
        /// wrong-case name, a numeric value and an unknown name are all defects.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type.</typeparam>
        /// <param name="key">The member key.</param>
        /// <returns>The enumeration member, or its default when a defect was recorded.</returns>
        public TEnum Enum<TEnum>(string key) where TEnum : struct, System.Enum
        {
            string name = String(key);
            if (name == null)
                return default(TEnum);

            if (!System.Enum.IsDefined(typeof(TEnum), name) || !System.Enum.TryParse(name, false, out TEnum value))
            {
                Record(key, "'" + name + "' is not a member of " + typeof(TEnum).Name + ".");
                return default(TEnum);
            }

            return value;
        }

        /// <summary>
        /// Reads an enumeration member that may be written as a JSON null. The member must still be
        /// present.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type.</typeparam>
        /// <param name="key">The member key.</param>
        /// <returns>The enumeration member, or null when the member is a JSON null or a defect was recorded.</returns>
        public TEnum? OptionalEnum<TEnum>(string key) where TEnum : struct, System.Enum
        {
            if (!TryMember(key, out JsonValue member))
                return null;

            if (member is JsonNull _)
                return null;

            TEnum value = Enum<TEnum>(key);
            return Failed ? (TEnum?)null : value;
        }

        /// <summary>
        /// Records a defect a codec found for itself, for a cross-check no member accessor can make.
        /// </summary>
        /// <param name="key">The member key the defect concerns.</param>
        /// <param name="reason">The reason, as a sentence.</param>
        public void Fail(string key, string reason) => Record(key, reason);

        /// <summary>
        /// Looks up a member, recording an absence as a defect and refusing to read once the reader has
        /// already failed.
        /// </summary>
        /// <param name="key">The member key.</param>
        /// <param name="value">The member value, or null.</param>
        /// <returns>True when the member was found and the reader had not already failed.</returns>
        private bool TryMember(string key, out JsonValue value)
        {
            value = null;
            if (Failed)
                return false;

            if (_source.TryGet(key, out value))
                return true;

            value = null;
            Record(key, "the member is absent.");
            return false;
        }

        /// <summary>
        /// Records a defect against this reader's path.
        /// </summary>
        /// <param name="key">The member key the defect concerns.</param>
        /// <param name="reason">The reason, as a sentence.</param>
        private void Record(string key, string reason)
        {
            _defects.Record(_path + "." + key + ": " + reason);
        }

        /// <summary>
        /// Holds the first defect recorded anywhere in a reader tree. Shared by a reader and every
        /// reader its <see cref="Child"/> produces, so the first defect wins across the whole graph.
        /// </summary>
        private sealed class DefectSink
        {
            /// <summary>
            /// The first defect recorded, or null while none has been.
            /// </summary>
            public string Defect { get; private set; }

            /// <summary>
            /// Records a defect unless one is already held.
            /// </summary>
            /// <param name="defect">The defect text.</param>
            public void Record(string defect) => Defect ??= defect;
        }
    }
}