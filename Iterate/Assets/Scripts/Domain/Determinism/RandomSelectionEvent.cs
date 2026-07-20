using System;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// A single RANDOM_SELECTION event payload: its kind, the selection identity, and a draw
    /// ordinal that is one or greater for <see cref="RandomSelectionEventKind.DrawResolved"/> and zero for
    /// every other kind. All members are scalar, so value equality is the synthesized record equality.
    /// </summary>
    public sealed record RandomSelectionEvent
    {
        /// <summary>
        /// The event kind.
        /// </summary>
        public RandomSelectionEventKind Kind { get; }

        /// <summary>
        /// The identity of the selection this event belongs to.
        /// </summary>
        public string SelectionIdentity { get; }

        /// <summary>
        /// The one-based draw ordinal for a draw-resolved event; zero for every other kind.
        /// </summary>
        public int DrawOrdinal { get; }

        public RandomSelectionEvent(RandomSelectionEventKind kind, string selectionIdentity, int drawOrdinal)
        {
            if (string.IsNullOrEmpty(selectionIdentity))
            {
                throw new ArgumentException(
                    "A selection event requires a selection identity.",
                    nameof(selectionIdentity)
                );
            }

            if (kind == RandomSelectionEventKind.DrawResolved)
            {
                if (drawOrdinal < 1)
                {
                    throw new ArgumentException(
                        "A draw-resolved event requires a draw ordinal of one or greater.",
                        nameof(drawOrdinal)
                    );
                }
            }
            else if (drawOrdinal != 0)
            {
                throw new ArgumentException(
                    "Only a draw-resolved event carries a nonzero draw ordinal.",
                    nameof(drawOrdinal)
                );
            }

            Kind = kind;
            SelectionIdentity = selectionIdentity;
            DrawOrdinal = drawOrdinal;
        }
    }
}