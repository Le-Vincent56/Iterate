using System;
using System.Globalization;
using Iterate.Domain.Compilation;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The deterministic engine-derived Structure identity strings: an entry identity composed from
    /// the Structure instance, its header position, and the entry ordinal; an iteration identity
    /// appending the iteration ordinal; and an evaluation identity appending the single per-entry
    /// evaluation marker. The formats are convention pinned by tests, invariant-culture throughout.
    /// </summary>
    public static class StructureIdentities
    {
        /// <summary>
        /// Composes a Structure entry identity: the instance, the header position, and the entry
        /// ordinal — <c>#5@3#1</c> for instance #5 entered at position 3 the first time.
        /// </summary>
        /// <param name="structureInstance">The Structure instance entering.</param>
        /// <param name="headerPosition">The header's source position.</param>
        /// <param name="entryOrdinal">The one-based entry ordinal.</param>
        /// <returns>The entry identity.</returns>
        /// <exception cref="ArgumentException">Thrown when the entry ordinal is below one.</exception>
        public static string Entry(
            InstanceID structureInstance,
            SourcePosition headerPosition,
            int entryOrdinal)
        {
            if (entryOrdinal < 1)
                throw new ArgumentException("An entry ordinal is one-based.", nameof(entryOrdinal));

            return structureInstance.ToString()
                + "@" + headerPosition.LineNumber.ToString(CultureInfo.InvariantCulture)
                + "#" + entryOrdinal.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Composes a Repeat iteration identity from the entry identity and the iteration ordinal —
        /// <c>#5@3#1/iter-2</c> for the entry's second iteration.
        /// </summary>
        /// <param name="entryIdentity">The owning entry identity.</param>
        /// <param name="iterationOrdinal">The one-based iteration ordinal.</param>
        /// <returns>The iteration identity.</returns>
        /// <exception cref="ArgumentException">Thrown when the iteration ordinal is below one.</exception>
        public static string Iteration(string entryIdentity, int iterationOrdinal)
        {
            if (iterationOrdinal < 1)
                throw new ArgumentException("An iteration ordinal is one-based.", nameof(iterationOrdinal));

            return entryIdentity + "/iter-" + iterationOrdinal.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Composes a Condition evaluation identity from the entry identity — <c>#5@3#1/eval-1</c>;
        /// one evaluation exists per entry.
        /// </summary>
        /// <param name="entryIdentity">The owning entry identity.</param>
        /// <returns>The evaluation identity.</returns>
        public static string Evaluation(string entryIdentity)
        {
            return entryIdentity + "/eval-1";
        }
    }
}