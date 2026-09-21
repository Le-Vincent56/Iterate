using System;
using System.Collections.Generic;

namespace Iterate.Application.Content.Json
{
    /// <summary>
    /// Constructs JSON value nodes for the writing side. Every node is built at line and column zero:
    /// the tree's positions describe where a value was read from, and a value being written was never
    /// read from anywhere.
    /// </summary>
    public static class JsonValues
    {
        /// <summary>
        /// Wraps a non-null string.
        /// </summary>
        /// <param name="value">The string value.</param>
        /// <returns>The string node.</returns>
        /// <exception cref="ArgumentException">Thrown when the value is null.</exception>
        public static JsonString String(string value)
        {
            if (value == null)
                throw new ArgumentException("a JSON string cannot be null; use Null() or StringOrNull().", nameof(value));

            return new JsonString(0, 0, value);
        }

        /// <summary>
        /// Wraps an integer, which writes without a fraction or exponent.
        /// </summary>
        /// <param name="value">The integer value.</param>
        /// <returns>The number node in integer form.</returns>
        public static JsonNumber Integer(long value) => new(0, 0, true, value, value);

        /// <summary>
        /// Wraps a double, which writes in "G17" form.
        /// </summary>
        /// <param name="value">The double value.</param>
        /// <returns>The number node in non-integer form.</returns>
        public static JsonNumber Double(double value) => new(0, 0, false, 0L, value);

        /// <summary>
        /// Wraps a boolean.
        /// </summary>
        /// <param name="value">The boolean value.</param>
        /// <returns>The boolean node.</returns>
        public static JsonBool Bool(bool value) => new(0, 0, value);

        /// <summary>
        /// Builds a JSON null literal.
        /// </summary>
        /// <returns>The null node.</returns>
        public static JsonNull Null() => new(0, 0);

        /// <summary>
        /// Wraps a list of items as an array, preserving their order.
        /// </summary>
        /// <param name="items">The array items in order.</param>
        /// <returns>The array node.</returns>
        /// <exception cref="ArgumentException">Thrown when the item list is null.</exception>
        public static JsonArray Array(IReadOnlyList<JsonValue> items)
        {
            if (items == null)
                throw new ArgumentException("a JSON array must have an item list.", nameof(items));

            return new JsonArray(0, 0, items);
        }

        /// <summary>
        /// Wraps a string that may be absent, mapping null to a JSON null literal.
        /// </summary>
        /// <param name="value">The string value, or null.</param>
        /// <returns>The string node, or the null node.</returns>
        public static JsonValue StringOrNull(string value) => value == null ? Null() : String(value);
    }
}