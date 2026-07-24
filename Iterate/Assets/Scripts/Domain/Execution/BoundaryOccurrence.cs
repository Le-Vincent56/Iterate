using System;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One reached named scheduling boundary offered to the effect engine, with the register
    /// snapshot boundary effects qualify against — the boundary reads current state, so parity and
    /// register qualifiers evaluate here rather than at authoring time.
    /// </summary>
    /// <param name="BoundaryName">The reached boundary's controlled token; never empty.</param>
    /// <param name="Value">The Value register's snapshot at the boundary.</param>
    /// <param name="Signal">The Signal register's snapshot at the boundary.</param>
    /// <param name="Score">The Score register's snapshot at the boundary.</param>
    public sealed record BoundaryOccurrence(
        string BoundaryName,
        int Value,
        int Signal,
        int Score
    )
    {
        /// <summary>
        /// The reached boundary's controlled token. Validated non-empty at construction.
        /// </summary>
        public string BoundaryName { get; } = RequireName(BoundaryName);

        /// <summary>
        /// Validates that the boundary name is present.
        /// </summary>
        /// <param name="boundaryName">The candidate name.</param>
        /// <returns>The name unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the name is null or empty.</exception>
        private static string RequireName(string boundaryName)
        {
            if (string.IsNullOrEmpty(boundaryName))
                throw new ArgumentException("A boundary occurrence requires its boundary name.", nameof(boundaryName));

            return boundaryName;
        }
    }
}