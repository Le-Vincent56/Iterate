using System;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// One Core-owned source line: a stable core-line identity paired with the typed operation the
    /// Execution Engine evaluates directly. Value equality compares the identity and the operation, so
    /// arrangement equivalence over Core lines stays a pure value comparison.
    /// </summary>
    /// <param name="Identity">The stable core-line identity; non-empty.</param>
    /// <param name="Operation">The typed operation this line applies; non-null.</param>
    public sealed record CoreLine(string Identity, CoreLineOperation Operation)
    {
        /// <summary>
        /// The stable core-line identity. Validated non-empty at construction.
        /// </summary>
        public string Identity { get; } = RequireText(Identity, nameof(Identity));

        /// <summary>
        /// The typed operation this line applies. Validated non-null at construction.
        /// </summary>
        public CoreLineOperation Operation { get; } = RequireOperation(Operation);

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

        /// <summary>
        /// Validates that the paired operation is present.
        /// </summary>
        /// <param name="operation">The candidate operation.</param>
        /// <returns>The operation unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the operation is null.</exception>
        private static CoreLineOperation RequireOperation(CoreLineOperation operation)
        {
            if (operation == null)
                throw new ArgumentException("A CoreLine requires an operation.", nameof(operation));

            return operation;
        }
    }
}