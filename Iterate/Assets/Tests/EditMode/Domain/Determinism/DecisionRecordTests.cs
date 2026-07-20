using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Determinism.Tests
{
    /// <summary>
    /// Tests the decision-record contract types (CAB-EVT-782/793/795/827). Covers construction validation
    /// (draw-ordinal floor, null-list rejection, the DrawResolved-only nonzero draw-ordinal rule), the
    /// first-class <see cref="RandomDecisionRecord.ResultPermutation"/>, and — critically — structural value
    /// equality across independently-built records with separate list instances (the equality the RND-08
    /// replay-by-recomputation sweep depends on).
    /// </summary>
    public sealed class DecisionRecordTests
    {
        [Test]
        public void DrawRecord_OrdinalBelowOne_Throws()
        {
            Assert.Throws<ArgumentException>(() => new DrawRecord(
                0, new List<string> { "a" }, "a", ReplacementBehavior.RemainsEligible, new List<string> { "a" }));
        }

        [Test]
        public void DrawRecord_NullPopulationBefore_Throws()
        {
            Assert.Throws<ArgumentException>(() => new DrawRecord(
                1, null, "a", ReplacementBehavior.RemainsEligible, new List<string> { "a" }));
        }

        [Test]
        public void DrawRecord_NullPopulationAfter_Throws()
        {
            Assert.Throws<ArgumentException>(() => new DrawRecord(
                1, new List<string> { "a" }, "a", ReplacementBehavior.RemainsEligible, null));
        }

        [Test]
        public void DrawRecord_EmptySelectedIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => new DrawRecord(
                1, new List<string> { "a" }, "", ReplacementBehavior.RemainsEligible, new List<string> { "a" }));
        }

        [Test]
        public void DrawRecord_ValidFields_RoundTrip()
        {
            // Arrange & Act
            DrawRecord record = new(
                2,
                new List<string> { "a", "b" },
                "a",
                ReplacementBehavior.RemovedForThisDecision,
                new List<string> { "b" });

            // Assert
            Assert.That(record.DrawOrdinal, Is.EqualTo(2));
            Assert.That(record.PopulationBefore, Is.EqualTo(new[] { "a", "b" }));
            Assert.That(record.SelectedIdentity, Is.EqualTo("a"));
            Assert.That(record.Consequence, Is.EqualTo(ReplacementBehavior.RemovedForThisDecision));
            Assert.That(record.PopulationAfter, Is.EqualTo(new[] { "b" }));
        }

        [Test]
        public void DrawRecord_IdenticallyBuiltWithSeparateLists_AreEqual()
        {
            DrawRecord first = new(
                1, new List<string> { "a", "b" }, "a", ReplacementBehavior.RemainsEligible, new List<string> { "a", "b" });
            DrawRecord second = new(
                1, new List<string> { "a", "b" }, "a", ReplacementBehavior.RemainsEligible, new List<string> { "a", "b" });

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void DrawRecord_DifferingSelectedIdentity_AreNotEqual()
        {
            DrawRecord first = new(
                1, new List<string> { "a", "b" }, "a", ReplacementBehavior.RemainsEligible, new List<string> { "a", "b" });
            DrawRecord second = new(
                1, new List<string> { "a", "b" }, "b", ReplacementBehavior.RemainsEligible, new List<string> { "a", "b" });

            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void DecisionOutcome_NullSelectedIdentities_Throws()
        {
            Assert.Throws<ArgumentException>(() => new DecisionOutcome(DecisionDisposition.Selected, null));
        }

        [Test]
        public void DecisionOutcome_EmptySelectedIdentities_IsAllowed()
        {
            DecisionOutcome outcome = new(DecisionDisposition.FailedToQualify, new List<string>());

            Assert.That(outcome.SelectedIdentities, Is.Empty);
            Assert.That(outcome.Disposition, Is.EqualTo(DecisionDisposition.FailedToQualify));
        }

        [Test]
        public void DecisionOutcome_IdenticallyBuiltWithSeparateLists_AreEqual()
        {
            DecisionOutcome first = new(DecisionDisposition.Selected, new List<string> { "a" });
            DecisionOutcome second = new(DecisionDisposition.Selected, new List<string> { "a" });

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void RandomSelectionEvent_DrawResolvedWithZeroOrdinal_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RandomSelectionEvent(
                RandomSelectionEventKind.DrawResolved, "sel", 0));
        }

        [Test]
        public void RandomSelectionEvent_DrawResolvedWithPositiveOrdinal_IsAllowed()
        {
            Assert.DoesNotThrow(() => new RandomSelectionEvent(RandomSelectionEventKind.DrawResolved, "sel", 1));
        }

        [Test]
        public void RandomSelectionEvent_NonDrawResolvedWithNonzeroOrdinal_Throws(
            [Values(RandomSelectionEventKind.DecisionCreated, RandomSelectionEventKind.CandidateSnapshotCaptured,
                RandomSelectionEventKind.SelectionCompleted, RandomSelectionEventKind.SelectionFailed,
                RandomSelectionEventKind.FallbackResolved)] RandomSelectionEventKind kind)
        {
            Assert.Throws<ArgumentException>(() => new RandomSelectionEvent(kind, "sel", 1));
        }

        [Test]
        public void RandomSelectionEvent_NonDrawResolvedWithZeroOrdinal_IsAllowed()
        {
            Assert.DoesNotThrow(() => new RandomSelectionEvent(RandomSelectionEventKind.SelectionCompleted, "sel", 0));
        }

        [Test]
        public void RandomSelectionEvent_EmptySelectionIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RandomSelectionEvent(
                RandomSelectionEventKind.DecisionCreated, "", 0));
        }

        [Test]
        public void RandomDecisionRecord_NullRequest_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RandomDecisionRecord(
                null, BuildEvidence(), "iterate-rng-1", BuildDraws(), new List<string>(), BuildOutcome()));
        }

        [Test]
        public void RandomDecisionRecord_EmptyRevisionIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RandomDecisionRecord(
                BuildRequest(), BuildEvidence(), "", BuildDraws(), new List<string>(), BuildOutcome()));
        }

        [Test]
        public void RandomDecisionRecord_NullDraws_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RandomDecisionRecord(
                BuildRequest(), BuildEvidence(), "iterate-rng-1", null, new List<string>(), BuildOutcome()));
        }

        [Test]
        public void RandomDecisionRecord_NullResultPermutation_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RandomDecisionRecord(
                BuildRequest(), BuildEvidence(), "iterate-rng-1", BuildDraws(), null, BuildOutcome()));
        }

        [Test]
        public void RandomDecisionRecord_NullOutcome_Throws()
        {
            Assert.Throws<ArgumentException>(() => new RandomDecisionRecord(
                BuildRequest(), BuildEvidence(), "iterate-rng-1", BuildDraws(), new List<string>(), null));
        }

        [Test]
        public void RandomDecisionRecord_Fields_RoundTrip()
        {
            RandomDecisionRecord record = BuildRecord();

            Assert.That(record.RevisionIdentity, Is.EqualTo("iterate-rng-1"));
            Assert.That(record.CandidateEvidence, Has.Count.EqualTo(2));
            Assert.That(record.Draws, Has.Count.EqualTo(1));
            Assert.That(record.ResultPermutation, Is.Empty);
            Assert.That(record.Outcome.Disposition, Is.EqualTo(DecisionDisposition.Selected));
        }

        [Test]
        public void RandomDecisionRecord_IdenticallyBuiltWithSeparateLists_AreEqual()
        {
            // The load-bearing case: two records built from separate list instances with equal content must
            // be value-equal — this is what replay-by-recomputation relies on.
            RandomDecisionRecord first = BuildRecord();
            RandomDecisionRecord second = BuildRecord();

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void RandomDecisionRecord_DifferingResultPermutation_AreNotEqual()
        {
            RandomDecisionRecord first = new(
                BuildRequest(), BuildEvidence(), "iterate-rng-1", BuildDraws(), new List<string> { "a", "b" }, BuildOutcome());
            RandomDecisionRecord second = new(
                BuildRequest(), BuildEvidence(), "iterate-rng-1", BuildDraws(), new List<string> { "b", "a" }, BuildOutcome());

            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void DecisionResult_NullMembers_Throw(
            [Values("outcome", "record", "events")] string nullField)
        {
            Assert.Throws<ArgumentException>(() => new DecisionResult(
                nullField == "outcome" ? null : BuildOutcome(),
                nullField == "record" ? null : BuildRecord(),
                nullField == "events" ? null : new List<RandomSelectionEvent>()));
        }

        [Test]
        public void DecisionResult_IdenticallyBuilt_AreEqual()
        {
            DecisionResult first = new(BuildOutcome(), BuildRecord(), BuildEvents());
            DecisionResult second = new(BuildOutcome(), BuildRecord(), BuildEvents());

            Assert.That(second, Is.EqualTo(first));
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

        private static List<RandomSelectionEvent> BuildEvents()
        {
            return new List<RandomSelectionEvent>
            {
                new RandomSelectionEvent(RandomSelectionEventKind.DecisionCreated, "sel-1", 0),
                new RandomSelectionEvent(RandomSelectionEventKind.CandidateSnapshotCaptured, "sel-1", 0),
                new RandomSelectionEvent(RandomSelectionEventKind.DrawResolved, "sel-1", 1),
                new RandomSelectionEvent(RandomSelectionEventKind.SelectionCompleted, "sel-1", 0)
            };
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
