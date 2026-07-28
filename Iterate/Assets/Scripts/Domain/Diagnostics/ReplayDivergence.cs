using System;

namespace Iterate.Domain.Diagnostics
{
    /// <summary>
    /// The identified locus of the first difference between stored and recomputed evidence: the area of
    /// the record that differs, the index of the first differing element for the six list-walked areas
    /// (-1 for the four scalar areas), and a composed description naming both sides. The comparer is the
    /// single owner of the description vocabulary; nothing else composes these strings.
    /// </summary>
    public sealed record ReplayDivergence
    {
        /// <summary>
        /// The area of the record in which the difference was found.
        /// </summary>
        public ReplayDivergenceArea Area { get; }

        /// <summary>
        /// The zero-based index of the first differing element for a list-walked area, or -1 for a
        /// scalar area.
        /// </summary>
        public int ElementIndex { get; }

        /// <summary>
        /// The composed description naming the stored and the recomputed value; never empty.
        /// </summary>
        public string Description { get; }

        public ReplayDivergence(ReplayDivergenceArea area, int elementIndex, string description)
        {
            bool isListArea;

            switch (area)
            {
                case ReplayDivergenceArea.Events:
                case ReplayDivergenceArea.Units:
                case ReplayDivergenceArea.TraversalOrder:
                case ReplayDivergenceArea.ThresholdHistory:
                case ReplayDivergenceArea.CounterHistory:
                case ReplayDivergenceArea.Defects:
                    isListArea = true;
                    break;

                case ReplayDivergenceArea.FinalState:
                case ReplayDivergenceArea.SafetyCounts:
                case ReplayDivergenceArea.CompletionStatus:
                case ReplayDivergenceArea.SafetyStatus:
                    isListArea = false;
                    break;

                default:
                    throw new ArgumentException("A divergence requires one of the ten walked areas.", nameof(area));
            }

            if (string.IsNullOrEmpty(description))
                throw new ArgumentException("A divergence requires a description.", nameof(description));

            if (isListArea && elementIndex < 0)
                throw new ArgumentException("A list area requires the zero-based index of the first differing element.", nameof(elementIndex));

            if (!isListArea && elementIndex != -1)
                throw new ArgumentException("A scalar area has no list to index into; supply -1.", nameof(elementIndex));

            Area = area;
            ElementIndex = elementIndex;
            Description = description;
        }
    }
}