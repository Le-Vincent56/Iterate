using System;
using System.Collections.Generic;
using Iterate.Domain.Compilation;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// A reconstructable configuration (UX-VAL-011): every source line, the Patches attached to
    /// installed Instructions, the pending pragmas, and the arrangement's state against the last
    /// compiled one. Equality compares every member, the lists element by element, so an arrangement
    /// moved away and back equals its earlier snapshot and is not written again.
    /// </summary>
    public sealed record ConfigurationSnapshot
    {
        /// <summary>
        /// Every source line, in line order.
        /// </summary>
        public IReadOnlyList<ConfigurationLine> Lines { get; }

        /// <summary>
        /// The attached Patches, in line then socket order.
        /// </summary>
        public IReadOnlyList<ConfigurationPatch> Patches { get; }

        /// <summary>
        /// The pending pragmas, in activation order.
        /// </summary>
        public IReadOnlyList<ConfigurationPragma> Pragmas { get; }

        /// <summary>
        /// The current arrangement against the last compiled one: <c>Initial</c> before the first
        /// compilation, <c>Unchanged</c> when it is the compiled one, otherwise how it was edited.
        /// </summary>
        public CompilationClassification State { get; }

        /// <summary>
        /// Creates a snapshot.
        /// </summary>
        /// <param name="lines">Every source line, in line order.</param>
        /// <param name="patches">The attached Patches.</param>
        /// <param name="pragmas">The pending pragmas.</param>
        /// <param name="state">The arrangement against the last compiled one.</param>
        /// <exception cref="ArgumentNullException">Thrown when a list is null.</exception>
        public ConfigurationSnapshot(
            IReadOnlyList<ConfigurationLine> lines,
            IReadOnlyList<ConfigurationPatch> patches,
            IReadOnlyList<ConfigurationPragma> pragmas,
            CompilationClassification state
        )
        {
            Lines = lines ?? throw new ArgumentNullException(nameof(lines));
            Patches = patches ?? throw new ArgumentNullException(nameof(patches));
            Pragmas = pragmas ?? throw new ArgumentNullException(nameof(pragmas));
            State = state;
        }

        /// <summary>
        /// Compares every member, the lists element by element.
        /// </summary>
        /// <param name="other">The snapshot to compare with.</param>
        /// <returns>Whether the two describe the same configuration.</returns>
        public bool Equals(ConfigurationSnapshot other)
        {
            if (other == null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return State == other.State
                && SequenceEqual(Lines, other.Lines)
                && SequenceEqual(Patches, other.Patches)
                && SequenceEqual(Pragmas, other.Pragmas);
        }

        /// <summary>
        /// Hashes every member, the lists element by element.
        /// </summary>
        /// <returns>The hash.</returns>
        public override int GetHashCode()
        {
            HashCode hash = new();
            hash.Add(State);
            AddAll(ref hash, Lines);
            AddAll(ref hash, Patches);
            AddAll(ref hash, Pragmas);
            return hash.ToHashCode();
        }

        /// <summary>
        /// Compares two lists element by element.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="first">The first list.</param>
        /// <param name="second">The second list.</param>
        /// <returns>Whether both hold equal elements in the same order.</returns>
        private static bool SequenceEqual<T>(IReadOnlyList<T> first, IReadOnlyList<T> second)
        {
            if (first.Count != second.Count)
                return false;

            for (int index = 0; index < first.Count; index++)
            {
                if (!EqualityComparer<T>.Default.Equals(first[index], second[index]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Adds every element of a list to a hash.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="hash">The hash to add to.</param>
        /// <param name="items">The list.</param>
        private static void AddAll<T>(ref HashCode hash, IReadOnlyList<T> items)
        {
            hash.Add(items.Count);
            for (int index = 0; index < items.Count; index++)
                hash.Add(items[index]);
        }
    }
}