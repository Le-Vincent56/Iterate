using System;
using Iterate.Domain.Diagnostics;
using NUnit.Framework;

namespace Iterate.Domain.Diagnostics.Tests
{
    /// <summary>
    /// Contract tests for <see cref="ReplayDivergence"/> and the <see cref="ReplayDivergenceArea"/> walk
    /// order: the ten areas appear in the canonical order the comparer walks them in, a list area carries
    /// a non-negative element index, a scalar area carries -1, and the composed description is never empty.
    /// </summary>
    public sealed class ReplayDivergenceTests
    {
        /// <summary>
        /// The six areas the comparer walks element-wise, each reporting the index of the first
        /// differing element.
        /// </summary>
        private static ReplayDivergenceArea[] ListAreas()
        {
            return new[]
            {
                ReplayDivergenceArea.Events,
                ReplayDivergenceArea.Units,
                ReplayDivergenceArea.TraversalOrder,
                ReplayDivergenceArea.ThresholdHistory,
                ReplayDivergenceArea.CounterHistory,
                ReplayDivergenceArea.Defects
            };
        }

        /// <summary>
        /// The four areas that compare as single values and so carry no element index.
        /// </summary>
        private static ReplayDivergenceArea[] ScalarAreas()
        {
            return new[]
            {
                ReplayDivergenceArea.FinalState,
                ReplayDivergenceArea.SafetyCounts,
                ReplayDivergenceArea.CompletionStatus,
                ReplayDivergenceArea.SafetyStatus
            };
        }

        [Test]
        public void Areas_AppearInCanonicalWalkOrder()
        {
            // Arrange — the walk order is public contract: the first area to differ is the divergence
            // that gets reported, so reordering these members silently changes what a comparison says.
            // Events precede units because an event index is the finest-grained chronology, which is
            // what CAB-PRES-112 means by the earliest difference. Completion status precedes safety
            // status because the record constructor cross-checks the pair, so a completion divergence
            // always co-occurs with a safety divergence; ordered the other way round the
            // CompletionStatus area would have no constructible producer at all.
            ReplayDivergenceArea[] expected =
            {
                ReplayDivergenceArea.Events,
                ReplayDivergenceArea.Units,
                ReplayDivergenceArea.TraversalOrder,
                ReplayDivergenceArea.ThresholdHistory,
                ReplayDivergenceArea.CounterHistory,
                ReplayDivergenceArea.Defects,
                ReplayDivergenceArea.FinalState,
                ReplayDivergenceArea.SafetyCounts,
                ReplayDivergenceArea.CompletionStatus,
                ReplayDivergenceArea.SafetyStatus
            };

            // Act
            Array actual = Enum.GetValues(typeof(ReplayDivergenceArea));

            // Assert — equality pins the order and the absence of an eleventh area in one assertion.
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void ListArea_WithElementIndex_Constructs()
        {
            // Act
            ReplayDivergence divergence = new(ReplayDivergenceArea.Events, 17, "EVENT[17]: stored A, recomputed B");

            // Assert
            Assert.That(divergence.Area, Is.EqualTo(ReplayDivergenceArea.Events));
            Assert.That(divergence.ElementIndex, Is.EqualTo(17));
            Assert.That(divergence.Description, Is.EqualTo("EVENT[17]: stored A, recomputed B"));
        }

        [Test]
        public void ListArea_WithZeroIndex_Constructs()
        {
            // Act — zero is the first element, not a missing index.
            ReplayDivergence divergence = new(ReplayDivergenceArea.Units, 0, "UNIT[0]: stored open, recomputed closed");

            // Assert
            Assert.That(divergence.ElementIndex, Is.EqualTo(0));
        }

        [Test]
        public void ScalarArea_WithoutElementIndex_Constructs()
        {
            // Act — -1 is the scalar areas' only legal index.
            ReplayDivergence divergence = new(ReplayDivergenceArea.FinalState, -1, "FINAL_STATE: stored Score 8, recomputed Score 12");

            // Assert
            Assert.That(divergence.Area, Is.EqualTo(ReplayDivergenceArea.FinalState));
            Assert.That(divergence.ElementIndex, Is.EqualTo(-1));
        }

        [Test]
        public void EveryListArea_RejectsAbsentElementIndex()
        {
            // Arrange & Act & Assert — a list divergence without an index cannot name its locus, which
            // is the whole obligation CAB-PRES-112 places on the report.
            foreach (ReplayDivergenceArea area in ListAreas())
            {
                Assert.Throws<ArgumentException>(
                    () => _ = new ReplayDivergence(area, -1, "a description"),
                    "Area {0} accepted an absent element index.",
                    area);
            }
        }

        [Test]
        public void EveryScalarArea_RejectsElementIndex()
        {
            // Arrange & Act & Assert — a scalar area has no list to index into, so any index is a defect
            // in the comparer rather than a locus a viewer could render.
            foreach (ReplayDivergenceArea area in ScalarAreas())
            {
                Assert.Throws<ArgumentException>(
                    () => _ = new ReplayDivergence(area, 0, "a description"),
                    "Area {0} accepted an element index.",
                    area);
            }
        }

        [Test]
        public void ListArea_WithIndexBelowMinusOne_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ReplayDivergence(ReplayDivergenceArea.Events, -2, "a description"));
        }

        [Test]
        public void UndefinedArea_Throws()
        {
            // A divergence outside the ten walked areas has no locus a viewer could render, so it is a
            // defect in the comparer rather than a report.
            Assert.Throws<ArgumentException>(() => _ = new ReplayDivergence((ReplayDivergenceArea)99, -1, "a description"));
        }

        [Test]
        public void EmptyDescription_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ReplayDivergence(ReplayDivergenceArea.Events, 0, ""));
        }

        [Test]
        public void NullDescription_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ReplayDivergence(ReplayDivergenceArea.Events, 0, null));
        }

        [Test]
        public void Equality_IsStructural()
        {
            // Arrange — two independently constructed divergences naming the same locus.
            ReplayDivergence left = new(ReplayDivergenceArea.ThresholdHistory, 3, "THRESHOLD_HISTORY[3]: stored 4 entries, recomputed 3");
            ReplayDivergence right = new(ReplayDivergenceArea.ThresholdHistory, 3, "THRESHOLD_HISTORY[3]: stored 4 entries, recomputed 3");

            // Act & Assert
            Assert.That(right, Is.EqualTo(left));
        }
    }
}
