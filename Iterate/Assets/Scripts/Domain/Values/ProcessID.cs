using System;

namespace Iterate.Domain.Values
{
    /// <summary>
    /// Identifies a Process. Wraps the stable string identity so bare strings never travel
    /// through gameplay signatures.
    /// </summary>
    public readonly record struct ProcessID
    {
        /// <summary>
        /// The raw stable identity string.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Returns the raw identity string.
        /// </summary>
        /// <returns>The identity exactly as constructed.</returns>
        public override string ToString() => Value;
        
        public ProcessID(string value)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("A ProcessID requires a non-empty identity.", nameof(value));

            Value = value;
        }
    }
}