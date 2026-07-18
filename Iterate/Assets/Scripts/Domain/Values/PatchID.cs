using System;

namespace Iterate.Domain.Values
{
    /// <summary>
    /// Identifies a Patch definition. Wraps the stable string identity so bare strings never travel
    /// through gameplay signatures.
    /// </summary>
    public readonly record struct PatchID
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
        
        public PatchID(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("A PatchID requires a non-empty identity.", nameof(value));
            }

            Value = value;
        }
    }
}