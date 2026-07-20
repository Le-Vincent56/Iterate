using System;
using System.Globalization;

namespace Iterate.Domain.Values
{
    /// <summary>
    /// Identifies a runtime instance, distinct from the identity of the definition it realises. Wraps a
    /// positive allocation ordinal so instance identity never travels through gameplay signatures as a
    /// bare integer.
    /// </summary>
    public readonly record struct InstanceID
    {
        /// <summary>
        /// The raw allocation ordinal; always one or greater after construction.
        /// </summary>
        public int Value { get; }

        public InstanceID(int value)
        {
            if (value < 1)
                throw new ArgumentException("An InstanceID requires a value of at least one.", nameof(value));

            Value = value;
        }

        /// <summary>
        /// Renders the identity as a hash-prefixed ordinal.
        /// </summary>
        /// <returns>The value prefixed with '#', formatted in the invariant culture.</returns>
        public override string ToString() => "#" + Value.ToString(CultureInfo.InvariantCulture);
    }
}