using System;
using System.Collections.Generic;
using Iterate.Domain.Determinism;
using Iterate.Domain.Trace;
using NUnit.Framework;

namespace Iterate.Domain.Trace.Tests
{
    /// <summary>
    /// Contract tests for <see cref="RandomDecisionPayload"/>: the wrapped decision record is required and
    /// preserved whole, and two payloads over structurally equal records compare equal so the record
    /// survives storage and replay-by-recomputation.
    /// </summary>
    public sealed class RandomDecisionPayloadTests
    {
        [Test]
        public void Construction_NullRecord_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new RandomDecisionPayload(null));
        }

        [Test]
        public void Construction_PreservesTheRecordWhole()
        {
            // Arrange
            RandomDecisionRecord record = BuildRecord();

            // Act
            RandomDecisionPayload payload = new(record);

            // Assert
            Assert.That(payload.Record, Is.SameAs(record));
        }

        [Test]
        public void Equality_IsStructuralOverTheRecord()
        {
            // Arrange — two payloads over independently built, structurally equal records.
            RandomDecisionPayload left = new(BuildRecord());
            RandomDecisionPayload right = new(BuildRecord());

            // Act & Assert
            Assert.That(right, Is.EqualTo(left));
        }

        private static DecisionContext BuildContext()
        {
            return DecisionContext.Derive(new DecisionContextComponents(
                "seed", null, null, null, null, null, null, null, "Random target selection", 1));
        }

        private static DecisionRequest BuildRequest()
        {
            return new DecisionRequest(
                "sel-1",
                "Random target selection",
                "boundary",
                "source",
                "eligibility",
                "at capture",
                "canonical",
                1,
                SelectionMethod.UniformSingleSelection,
                ReplacementBehavior.RemainsEligible,
                null,
                "tiebreak",
                null,
                InsufficientCandidateBehavior.FailToQualify,
                BuildContext());
        }

        private static List<CandidateEntry> BuildEvidence()
        {
            return new List<CandidateEntry>
            {
                new CandidateEntry("a", new CandidateOrderingKey("A", null, null, null), null),
                new CandidateEntry("b", new CandidateOrderingKey("B", null, null, null), null)
            };
        }

        private static List<DrawRecord> BuildDraws()
        {
            return new List<DrawRecord>
            {
                new DrawRecord(
                    1,
                    new List<string> { "a", "b" },
                    "a",
                    ReplacementBehavior.RemainsEligible,
                    new List<string> { "a", "b" })
            };
        }

        private static DecisionOutcome BuildOutcome()
        {
            return new DecisionOutcome(DecisionDisposition.Selected, new List<string> { "a" });
        }

        private static RandomDecisionRecord BuildRecord()
        {
            return new RandomDecisionRecord(
                BuildRequest(),
                BuildEvidence(),
                "iterate-rng-1",
                BuildDraws(),
                new List<string>(),
                BuildOutcome());
        }
    }
}
