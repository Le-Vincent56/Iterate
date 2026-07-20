using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Determinism.Tests
{
    /// <summary>
    /// Tests weighted selection (CAB-EVT-817/818/819/820). Weighted draws use exact final weights;
    /// zero-weight candidates stay in evidence but are never drawn (CAB-EVT-819); zero total weight routes
    /// through the declared insufficiency behavior, never a silent uniform draw (CAB-EVT-820). Under
    /// Vincent's ruling (July 20, 2026) zero total weight with SELECT ALL REMAINING selects every candidate.
    /// Draw selections were reproduced from the reference chain (session "seed", purpose "Random ordering").
    /// </summary>
    public sealed class DeterminismServiceWeightedTests
    {
        [Test]
        public void Decide_Weighted_NeverSelectsZeroWeightCandidateButKeepsItInEvidence()
        {
            // Arrange — weights {c0:5, c1:0, c2:3}; the reference draw lands on c2, and c1 is unselectable.
            DeterminismService service = new();
            DecisionRequest request = WeightedRequest(1, 1, ReplacementBehavior.RemainsEligible);
            CandidateSnapshot snapshot = BuildWeightedSnapshot(new int[] { 5, 0, 3 });

            // Act
            DecisionResult result = service.Decide(request, snapshot);

            // Assert
            Assert.That(result.Outcome.SelectedIdentities, Is.EqualTo(new[] { "c2" }));
            Assert.That(result.Outcome.SelectedIdentities, Does.Not.Contain("c1"));
            Assert.That(result.Record.CandidateEvidence[1].Identity, Is.EqualTo("c1"));
            Assert.That(result.Record.CandidateEvidence[1].FinalWeight, Is.EqualTo(0));
        }

        [Test]
        public void Decide_Weighted_RepeatedDecisions_AlwaysAvoidZeroWeight()
        {
            DeterminismService service = new();
            CandidateSnapshot snapshot = BuildWeightedSnapshot(new int[] { 5, 0, 3 });

            for (int attempt = 0; attempt < 4; attempt++)
            {
                DecisionResult result = service.Decide(
                    WeightedRequest(1, 1, ReplacementBehavior.RemainsEligible), snapshot);
                Assert.That(result.Outcome.SelectedIdentities, Does.Not.Contain("c1"), $"attempt {attempt}");
            }
        }

        [Test]
        public void Decide_WeightedWithoutReplacement_TwoOfThree_YieldsTwoDistinct()
        {
            // weights {5,2,3}, count 2, ordinal 1 -> pinned draw order c2, c0.
            DeterminismService service = new();
            DecisionRequest request = WeightedRequest(2, 1, ReplacementBehavior.RemovedForThisDecision);
            CandidateSnapshot snapshot = BuildWeightedSnapshot(new int[] { 5, 2, 3 });

            DecisionResult result = service.Decide(request, snapshot);

            Assert.That(result.Outcome.SelectedIdentities, Is.EqualTo(new[] { "c2", "c0" }));
            Assert.That(result.Record.Draws, Has.Count.EqualTo(2));
        }

        [Test]
        public void Decide_WeightedZeroTotal_FailToQualify_Fails()
        {
            DeterminismService service = new();
            DecisionRequest request = WeightedRequest(1, 1, ReplacementBehavior.RemainsEligible,
                InsufficientCandidateBehavior.FailToQualify);
            CandidateSnapshot snapshot = BuildWeightedSnapshot(new int[] { 0, 0 });

            DecisionResult result = service.Decide(request, snapshot);

            Assert.That(result.Outcome.Disposition, Is.EqualTo(DecisionDisposition.FailedToQualify));
            Assert.That(result.Outcome.SelectedIdentities, Is.Empty);
            Assert.That(result.Record.Draws, Is.Empty);
        }

        [Test]
        public void Decide_WeightedZeroTotal_SelectAllRemaining_SelectsAllInCanonicalOrder()
        {
            // Vincent's ruling: zero total weight with SELECT ALL REMAINING selects every candidate.
            DeterminismService service = new();
            DecisionRequest request = WeightedRequest(1, 1, ReplacementBehavior.RemainsEligible,
                InsufficientCandidateBehavior.SelectAllRemainingCandidates);
            CandidateSnapshot snapshot = BuildWeightedSnapshot(new int[] { 0, 0 });

            DecisionResult result = service.Decide(request, snapshot);

            Assert.That(result.Outcome.Disposition, Is.EqualTo(DecisionDisposition.SelectedAllRemaining));
            Assert.That(result.Outcome.SelectedIdentities, Is.EqualTo(new[] { "c0", "c1" }));
        }

        [Test]
        public void Decide_Weighted_IsDeterministicAcrossCalls()
        {
            DeterminismService service = new();
            CandidateSnapshot snapshot = BuildWeightedSnapshot(new int[] { 5, 2, 3 });

            DecisionResult first = service.Decide(
                WeightedRequest(2, 1, ReplacementBehavior.RemovedForThisDecision), snapshot);
            DecisionResult second = service.Decide(
                WeightedRequest(2, 1, ReplacementBehavior.RemovedForThisDecision), snapshot);

            Assert.That(second, Is.EqualTo(first));
        }

        private static CandidateSnapshot BuildWeightedSnapshot(int[] weights)
        {
            List<CandidateEntry> entries = new();
            for (int index = 0; index < weights.Length; index++)
            {
                string identity = "c" + index;
                entries.Add(new CandidateEntry(
                    identity, new CandidateOrderingKey(identity, null, null, null), weights[index]));
            }

            return CandidateSnapshot.Create(entries);
        }

        private static DecisionContext BuildContext(int ordinal)
        {
            return DecisionContext.Derive(new DecisionContextComponents(
                "seed", null, null, null, null, null, null, null, "Random ordering", ordinal));
        }

        private static DecisionRequest WeightedRequest(
            int count,
            int ordinal,
            ReplacementBehavior replacement,
            InsufficientCandidateBehavior insufficient = InsufficientCandidateBehavior.FailToQualify)
        {
            return new DecisionRequest(
                "sel-1",
                "Random ordering",
                "boundary",
                "source",
                "eligibility",
                "at capture",
                "canonical",
                count,
                SelectionMethod.WeightedSelection,
                replacement,
                "by weight",
                "tiebreak",
                null,
                insufficient,
                BuildContext(ordinal));
        }
    }
}
