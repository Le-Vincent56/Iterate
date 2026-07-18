using System;

namespace Iterate.Application.Logging
{
    /// <summary>
    /// One log occurrence traveling through the seam: category, level, message, optional
    /// exception, and up to three structured fields held inline so no array is allocated.
    /// </summary>
    /// <param name="Category">The category of the logger that produced the entry.</param>
    /// <param name="Level">The entry's severity.</param>
    /// <param name="Message">The human-readable message.</param>
    /// <param name="Exception">The carried exception; null except for Error entries.</param>
    /// <param name="Field0">The first structured field when <paramref name="FieldCount"/> is at least 1.</param>
    /// <param name="Field1">The second structured field when <paramref name="FieldCount"/> is at least 2.</param>
    /// <param name="Field2">The third structured field when <paramref name="FieldCount"/> is 3.</param>
    /// <param name="FieldCount">How many of the field slots are populated (0..3).</param>
    public readonly record struct LogEntry(
        LogCategory Category,
        LogLevel Level,
        string Message,
        Exception Exception,
        LogField Field0,
        LogField Field1,
        LogField Field2,
        int FieldCount
    )
    {
        /// <summary>
        /// Returns the populated field at the given index.
        /// </summary>
        /// <param name="index">The field index, 0 to <see cref="FieldCount"/> minus one.</param>
        /// <returns>The field at that index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is outside 0..2.</exception>
        public LogField FieldAt(int index)
        {
            return index switch
            {
                0 => Field0,
                1 => Field1,
                2 => Field2,
                _ => throw new ArgumentOutOfRangeException(nameof(index), index, "LogEntry holds at most three fields.")
            };
        }
    }
}