using System;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// A fixed, one-based numbered position in a source arrangement. Positions never renumber or reflow
    /// (UX-SRC-002); a position identifies a slot, not its occupant.
    /// </summary>
    public readonly record struct SourcePosition
    {
        /// <summary>
        /// The one-based line number; always one or greater after construction.
        /// </summary>
        public int LineNumber { get; }

        public SourcePosition(int lineNumber)
        {
            if (lineNumber < 1)
                throw new ArgumentException("A SourcePosition requires a line number of at least one.", nameof(lineNumber));

            LineNumber = lineNumber;
        }
    }
}