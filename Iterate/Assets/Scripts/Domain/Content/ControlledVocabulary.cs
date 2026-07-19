using System;
using System.Collections;
using System.Collections.Generic;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A closed, case-sensitive controlled-vocabulary set with O(1) membership. Exposes membership,
    /// count, and enumeration through its own members so callers never reach for System.Linq.
    /// </summary>
    public sealed class ControlledVocabulary : IEnumerable<string>
    {
        private readonly HashSet<string> _values;

        /// <summary>
        /// The number of controlled values in the set.
        /// </summary>
        public int Count => _values.Count;

        public ControlledVocabulary(params string[] values)
        {
            _values = new HashSet<string>(values, StringComparer.Ordinal);
        }

        /// <summary>
        /// Whether the given value is a controlled member of this set.
        /// </summary>
        /// <param name="value">The value to test.</param>
        /// <returns>True when the value is a member.</returns>
        public bool Contains(string value) => _values.Contains(value);

        /// <summary>
        /// Returns an enumerator over the controlled values.
        /// </summary>
        /// <returns>The enumerator over the set's values.</returns>
        public IEnumerator<string> GetEnumerator() => _values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _values.GetEnumerator();
    }
}