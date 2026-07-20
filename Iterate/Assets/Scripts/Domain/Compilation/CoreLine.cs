using System;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// One Core-owned source line: a stable core-line identity paired with an authored operation payload
    /// treated as an opaque immutable value by this feature. The payload participates in equivalence and
    /// is carried into a compiled source; executing it belongs to the Execution Engine, which finalizes
    /// the payload format.
    /// </summary>
    /// <param name="Identity">The stable core-line identity; non-empty.</param>
    /// <param name="OperationText">The opaque operation payload; non-empty.</param>
    public sealed record CoreLine(string Identity, string OperationText)
    {
        /// <summary>
        /// The stable core-line identity. Validated non-empty at construction.
        /// </summary>
        public string Identity { get; } = RequireText(Identity, nameof(Identity));

        /// <summary>
        /// The opaque operation payload. Validated non-empty at construction.
        /// </summary>
        public string OperationText { get; } = RequireText(OperationText, nameof(OperationText));

        /// <summary>
        /// Validates that a supplied text field is present and non-empty.
        /// </summary>
        /// <param name="value">The candidate text.</param>
        /// <param name="fieldName">The field name reported on failure.</param>
        /// <returns>The text unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the text is null or empty.</exception>
        private static string RequireText(string value, string fieldName)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("A CoreLine requires non-empty text.", fieldName);

            return value;
        }
    }
}