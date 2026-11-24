using System;
using System.Globalization;
using System.Text;

namespace Iterate.Application.Content.Json
{
    /// <summary>
    /// Serializes a JSON value tree to deterministic text: object members in <see cref="JsonObject.Keys"/>
    /// order, a two-space indent per level, '\n' line ends and a single trailing newline, empty objects
    /// and arrays on one line, numbers in the invariant culture, and control characters escaped. The
    /// same tree always produces the same text, which is what makes a generated artifact diffable.
    /// </summary>
    public static class JsonWriter
    {
        private const string IndentStep = "  ";

        /// <summary>
        /// Serializes a JSON value tree to text.
        /// </summary>
        /// <param name="value">The root value to serialize.</param>
        /// <returns>The document text, ending in a single newline.</returns>
        /// <exception cref="ArgumentException">Thrown when the value is null or of an unsupported kind.</exception>
        public static string Write(JsonValue value)
        {
            if (value == null)
                throw new ArgumentException("a JSON document must have a root value.", nameof(value));

            StringBuilder builder = new();
            WriteValue(builder, value, 0);
            builder.Append('\n');
            return builder.ToString();
        }

        /// <summary>
        /// Serializes a JSON value tree to one compact line: no whitespace between tokens, the same
        /// member order, escaping and invariant-culture numbers as <see cref="Write"/>, and a single
        /// trailing newline. A string's own line breaks are escaped, so the text holds no other newline.
        /// </summary>
        /// <param name="value">The root value to serialize.</param>
        /// <returns>The line, ending in a single newline.</returns>
        /// <exception cref="ArgumentException">Thrown when the value is null or of an unsupported kind.</exception>
        public static string WriteLine(JsonValue value)
        {
            if (value == null)
                throw new ArgumentException("a JSON line must have a root value.", nameof(value));

            StringBuilder builder = new();
            WriteCompactValue(builder, value);
            builder.Append('\n');
            return builder.ToString();
        }

        /// <summary>
        /// Writes one value at the given nesting depth.
        /// </summary>
        /// <param name="builder">The document builder.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="depth">The nesting depth, zero at the root.</param>
        private static void WriteValue(StringBuilder builder, JsonValue value, int depth)
        {
            switch (value)
            {
                case JsonObject asObject:
                    WriteObject(builder, asObject, depth);
                    break;

                case JsonArray asArray:
                    WriteArray(builder, asArray, depth);
                    break;

                case JsonString asString:
                    WriteString(builder, asString.Value);
                    break;

                case JsonNumber asNumber:
                    WriteNumber(builder, asNumber);
                    break;

                case JsonBool asBool:
                    builder.Append(asBool.Value ? "true" : "false");
                    break;

                case JsonNull _:
                    builder.Append("null");
                    break;

                default:
                    throw new ArgumentException($"unsupported JSON value kind '{value.GetType().Name}'.", nameof(value));
            }
        }

        /// <summary>
        /// Writes an object, its members in key order, empty on a single line.
        /// </summary>
        /// <param name="builder">The document builder.</param>
        /// <param name="value">The object to write.</param>
        /// <param name="depth">The nesting depth of the object itself.</param>
        private static void WriteObject(StringBuilder builder, JsonObject value, int depth)
        {
            if (value.Keys.Count == 0)
            {
                builder.Append("{}");
                return;
            }

            builder.Append("{\n");
            for (int index = 0; index < value.Keys.Count; index++)
            {
                string key = value.Keys[index];
                if (!value.TryGet(key, out JsonValue member))
                    throw new ArgumentException($"the object lists key '{key}' with no member.", nameof(value));

                AppendIndent(builder, depth + 1);
                WriteString(builder, key);
                builder.Append(": ");
                WriteValue(builder, member, depth + 1);
                if (index < value.Keys.Count - 1)
                    builder.Append(',');

                builder.Append('\n');
            }

            AppendIndent(builder, depth);
            builder.Append('}');
        }

        /// <summary>
        /// Writes an array, one item per line, empty on a single line.
        /// </summary>
        /// <param name="builder">The document builder.</param>
        /// <param name="value">The array to write.</param>
        /// <param name="depth">The nesting depth of the array itself.</param>
        private static void WriteArray(StringBuilder builder, JsonArray value, int depth)
        {
            if (value.Items.Count == 0)
            {
                builder.Append("[]");
                return;
            }

            builder.Append("[\n");
            for (int index = 0; index < value.Items.Count; index++)
            {
                AppendIndent(builder, depth + 1);
                WriteValue(builder, value.Items[index], depth + 1);
                if (index < value.Items.Count - 1)
                    builder.Append(',');

                builder.Append('\n');
            }

            AppendIndent(builder, depth);
            builder.Append(']');
        }

        /// <summary>
        /// Writes a number in its lexical form: an integer node as invariant decimal, any other as
        /// invariant "G17", which round-trips a double exactly on every runtime.
        /// </summary>
        /// <param name="builder">The document builder.</param>
        /// <param name="value">The number to write.</param>
        private static void WriteNumber(StringBuilder builder, JsonNumber value)
        {
            if (value.IsInteger)
            {
                builder.Append(value.IntegerValue.ToString(CultureInfo.InvariantCulture));
                return;
            }

            builder.Append(value.DoubleValue.ToString("G17", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Writes a quoted string, escaping the characters the reader refuses to see raw and passing
        /// every other character, including non-ASCII, through verbatim for UTF-8.
        /// </summary>
        /// <param name="builder">The document builder.</param>
        /// <param name="value">The string value to write.</param>
        private static void WriteString(StringBuilder builder, string value)
        {
            builder.Append('"');
            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                switch (current)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;

                    case '\\':
                        builder.Append("\\\\");
                        break;

                    case '\n':
                        builder.Append("\\n");
                        break;

                    case '\r':
                        builder.Append("\\r");
                        break;

                    case '\t':
                        builder.Append("\\t");
                        break;

                    case '\b':
                        builder.Append("\\b");
                        break;

                    case '\f':
                        builder.Append("\\f");
                        break;

                    case < ' ':
                        builder.Append("\\u");
                        builder.Append(((int)current).ToString("X4", CultureInfo.InvariantCulture));
                        break;

                    default:
                        builder.Append(current);
                        break;
                }
            }

            builder.Append('"');
        }

        /// <summary>
        /// Writes one value with no whitespace between tokens.
        /// </summary>
        /// <param name="builder">The line builder.</param>
        /// <param name="value">The value to write.</param>
        private static void WriteCompactValue(StringBuilder builder, JsonValue value)
        {
            switch (value)
            {
                case JsonObject asObject:
                    WriteCompactObject(builder, asObject);
                    break;

                case JsonArray asArray:
                    WriteCompactArray(builder, asArray);
                    break;

                case JsonString asString:
                    WriteString(builder, asString.Value);
                    break;

                case JsonNumber asNumber:
                    WriteNumber(builder, asNumber);
                    break;

                case JsonBool asBool:
                    builder.Append(asBool.Value ? "true" : "false");
                    break;

                case JsonNull _:
                    builder.Append("null");
                    break;

                default:
                    throw new ArgumentException($"unsupported JSON value kind '{value.GetType().Name}'.", nameof(value));
            }
        }

        /// <summary>
        /// Writes an object's members in key order, with no whitespace between tokens.
        /// </summary>
        /// <param name="builder">The line builder.</param>
        /// <param name="value">The object to write.</param>
        private static void WriteCompactObject(StringBuilder builder, JsonObject value)
        {
            builder.Append('{');
            for (int index = 0; index < value.Keys.Count; index++)
            {
                string key = value.Keys[index];
                if (!value.TryGet(key, out JsonValue member))
                    throw new ArgumentException($"the object lists key '{key}' with no member.", nameof(value));

                if (index > 0)
                    builder.Append(',');

                WriteString(builder, key);
                builder.Append(':');
                WriteCompactValue(builder, member);
            }

            builder.Append('}');
        }

        /// <summary>
        /// Writes an array's items in order, with no whitespace between tokens.
        /// </summary>
        /// <param name="builder">The line builder.</param>
        /// <param name="value">The array to write.</param>
        private static void WriteCompactArray(StringBuilder builder, JsonArray value)
        {
            builder.Append('[');
            for (int index = 0; index < value.Items.Count; index++)
            {
                if (index > 0)
                    builder.Append(',');

                WriteCompactValue(builder, value.Items[index]);
            }

            builder.Append(']');
        }

        /// <summary>
        /// Appends the indentation for a nesting depth.
        /// </summary>
        /// <param name="builder">The document builder.</param>
        /// <param name="depth">The nesting depth to indent to.</param>
        private static void AppendIndent(StringBuilder builder, int depth)
        {
            for (int level = 0; level < depth; level++)
            {
                builder.Append(IndentStep);
            }
        }
    }
}