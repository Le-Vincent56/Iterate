using System;
using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A frozen Core definition: the immutable line composition a Process executes, its open
    /// positions, and the designated final Core output. Line positions are contiguous from one and
    /// the final output names a fixed-instruction line, so the designated output boundary the
    /// Execution Engine reads always resolves. Not a content item: a Core carries no rules text,
    /// category, rarity or tags.
    /// </summary>
    /// <param name="ID">The Core's surrogate-key identity.</param>
    /// <param name="DisplayName">The display name shown to the player.</param>
    /// <param name="Lines">The authored Core lines in position order.</param>
    /// <param name="FinalOutputPosition">The position of the designated final Core output.</param>
    public sealed record CoreDefinition(
        CoreID ID,
        string DisplayName,
        IReadOnlyList<CoreLineSpec> Lines,
        SourcePosition FinalOutputPosition
    )
    {
        /// <summary>
        /// The authored Core lines in position order. Validated at construction: present, non-empty,
        /// contiguous from position one, and with the final output naming a fixed-instruction line.
        /// </summary>
        public IReadOnlyList<CoreLineSpec> Lines { get; } = RequireLines(Lines, FinalOutputPosition);

        /// <summary>
        /// The number of open positions, which is the Core's source capacity.
        /// </summary>
        public int OpenCount { get; } = CountOpen(Lines);

        /// <summary>
        /// Validates the line composition against the designated final output and returns it unchanged.
        /// </summary>
        /// <param name="lines">The candidate line list.</param>
        /// <param name="finalOutputPosition">The candidate final-output position.</param>
        /// <returns>The line list unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the composition or the final output is malformed.</exception>
        private static IReadOnlyList<CoreLineSpec> RequireLines(
            IReadOnlyList<CoreLineSpec> lines,
            SourcePosition finalOutputPosition
        )
        {
            if (lines == null)
                throw new ArgumentException("A CoreDefinition requires a line list.", nameof(lines));

            if (lines.Count == 0)
                throw new ArgumentException("A CoreDefinition requires at least one line.", nameof(lines));

            for (int index = 0; index < lines.Count; index++)
            {
                CoreLineSpec line = lines[index];
                if (line == null)
                    throw new ArgumentException("A CoreDefinition requires every line to be present.", nameof(lines));

                if (line.Position != index + 1)
                    throw new ArgumentException("A CoreDefinition requires contiguous one-based line positions.", nameof(lines));
            }

            int finalOutput = finalOutputPosition.LineNumber;
            if (finalOutput > lines.Count)
                throw new ArgumentException("A CoreDefinition's final output must name one of its own lines.", nameof(finalOutputPosition));

            if (lines[finalOutput - 1].Kind != CoreLineKind.FixedInstruction)
                throw new ArgumentException("A CoreDefinition's final output must name a fixed-instruction line.", nameof(finalOutputPosition));

            return lines;
        }

        /// <summary>
        /// Counts the open positions in a validated line composition.
        /// </summary>
        /// <param name="lines">The validated line list.</param>
        /// <returns>The number of open lines.</returns>
        private static int CountOpen(IReadOnlyList<CoreLineSpec> lines)
        {
            int open = 0;
            foreach (CoreLineSpec line in lines)
            {
                if (line.Kind == CoreLineKind.Open)
                    open++;
            }

            return open;
        }
    }
}