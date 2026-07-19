using System;

namespace Iterate.Domain.Values
{
    /// <summary>
    /// Identifies a Structure definition. Wraps the stable string identity so bare strings never
    /// travel through gameplay signatures.
    /// </summary>
    public readonly record struct StructureID
    {
        /// <summary>
        /// The raw stable identity string.
        /// </summary>
        public string Value { get; }

        public StructureID(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("A StructureID requires a non-empty identity.", nameof(value));
            }

            Value = value;
        }

        /// <summary>
        /// Returns the raw identity string.
        /// </summary>
        /// <returns>The identity exactly as constructed.</returns>
        public override string ToString() => Value;
    }
}