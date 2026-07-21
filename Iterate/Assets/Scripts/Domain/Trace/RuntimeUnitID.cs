using System;
using System.Globalization;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The index-stable identity of one runtime-unit record within one execution's evidence: a
    /// one-based ordinal minted in unit-open order. Identities are never reassigned within an
    /// execution and are not player-facing content identifiers.
    /// </summary>
    public readonly record struct RuntimeUnitID
    {
        /// <summary>
        /// The one-based mint ordinal; always one or greater after construction.
        /// </summary>
        public int Value { get; }

        public RuntimeUnitID(int value)
        {
            if (value < 1)
                throw new ArgumentException("A RuntimeUnitID requires a value of at least one.", nameof(value));

            Value = value;
        }

        /// <summary>
        /// Renders the identity as a U-prefixed ordinal.
        /// </summary>
        /// <returns>The value prefixed with 'U', formatted in the invariant culture.</returns>
        public override string ToString() => "U" + Value.ToString(CultureInfo.InvariantCulture);
    }
}