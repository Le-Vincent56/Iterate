namespace Iterate.Application.Logging
{
    /// <summary>
    /// A structured key/value pair attached to a log entry. A typed tagged union: the typed
    /// factory overloads store into value slots without boxing; the object overload is the
    /// boxed cold-path fallback.
    /// </summary>
    /// <param name="Key">The field's key.</param>
    /// <param name="Kind">Which slot carries the value.</param>
    /// <param name="StringValue">The value when <paramref name="Kind"/> is String; otherwise null.</param>
    /// <param name="Int64Value">The value when <paramref name="Kind"/> is Int64, or 1/0 for Bool.</param>
    /// <param name="DoubleValue">The value when <paramref name="Kind"/> is Double.</param>
    /// <param name="BoxedValue">The value when <paramref name="Kind"/> is Boxed; otherwise null.</param>
    public readonly record struct LogField(
        string Key,
        LogFieldKind Kind,
        string StringValue,
        long Int64Value,
        double DoubleValue,
        object BoxedValue
    )
    {
        /// <summary>
        /// Creates a string field.
        /// </summary>
        /// <param name="key">The field's key.</param>
        /// <param name="value">The string value.</param>
        /// <returns>The created field.</returns>
        public static LogField Of(string key, string value) => new(key, LogFieldKind.String, value, 0L, 0d, null);

        /// <summary>
        /// Creates an integer field without boxing.
        /// </summary>
        /// <param name="key">The field's key.</param>
        /// <param name="value">The integer value.</param>
        /// <returns>The created field.</returns>
        public static LogField Of(string key, int value) => new(key, LogFieldKind.Int64, null, value, 0d, null);

        /// <summary>
        /// Creates a long field without boxing.
        /// </summary>
        /// <param name="key">The field's key.</param>
        /// <param name="value">The long value.</param>
        /// <returns>The created field.</returns>
        public static LogField Of(string key, long value) => new(key, LogFieldKind.Int64, null, value, 0d, null);

        /// <summary>
        /// Creates a float field without boxing; stored at double precision.
        /// </summary>
        /// <param name="key">The field's key.</param>
        /// <param name="value">The float value.</param>
        /// <returns>The created field.</returns>
        public static LogField Of(string key, float value) => new(key, LogFieldKind.Double, null, 0L, value, null);

        /// <summary>
        /// Creates a double field without boxing.
        /// </summary>
        /// <param name="key">The field's key.</param>
        /// <param name="value">The double value.</param>
        /// <returns>The created field.</returns>
        public static LogField Of(string key, double value) => new(key, LogFieldKind.Double, null, 0L, value, null);

        /// <summary>
        /// Creates a boolean field without boxing; stored as 1 or 0 in the integer slot.
        /// </summary>
        /// <param name="key">The field's key.</param>
        /// <param name="value">The boolean value.</param>
        /// <returns>The created field.</returns>
        public static LogField Of(string key, bool value) => new(key, LogFieldKind.Bool, null, value ? 1L : 0L, 0d, null);

        /// <summary>
        /// Creates a boxed field for enums and rich values. Cold path: prefer the typed overloads.
        /// </summary>
        /// <param name="key">The field's key.</param>
        /// <param name="value">The value to box.</param>
        /// <returns>The created field.</returns>
        public static LogField Of(string key, object value) => new(key, LogFieldKind.Boxed, null, 0L, 0d, value);
    }
}