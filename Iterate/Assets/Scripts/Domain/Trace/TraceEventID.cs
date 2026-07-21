using System;
using System.Globalization;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The index-stable identity of one event record within one execution's evidence: a one-based
    /// ordinal minted in append order, so the identity is also the record's canonical chronological
    /// order. Identities are never reassigned within an execution and are not player-facing content
    /// identifiers.
    /// </summary>
    public readonly record struct TraceEventID
    {
        /// <summary>
        /// The one-based append ordinal; always one or greater after construction.
        /// </summary>
        public int Value { get; }

        public TraceEventID(int value)
        {
            if (value < 1)
                throw new ArgumentException("A TraceEventID requires a value of at least one.", nameof(value));

            Value = value;
        }

        /// <summary>
        /// Renders the identity as an E-prefixed ordinal.
        /// </summary>
        /// <returns>The value prefixed with 'E', formatted in the invariant culture.</returns>
        public override string ToString() => "E" + Value.ToString(CultureInfo.InvariantCulture);
    }
}