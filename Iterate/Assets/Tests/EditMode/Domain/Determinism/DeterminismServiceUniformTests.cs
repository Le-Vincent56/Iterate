using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Determinism.Tests
{
    /// <summary>
    /// Tests <see cref="DeterminismService.Decide"/> for the three uniform methods and the pipeline's
    /// determinism, event assembly, and request/snapshot compatibility. Known-answer selections were
    /// reproduced from the reference algorithm chain (canonical encoding → reference-verified xxHash64 seed
    /// 0 → PCG-XSH-RR 32 seeded with the fixed stream selector 54 → unbiased bounded draw) — never from
    /// memory. Snapshots present identities c0..cN in canonical order.
    /// </summary>
    public sealed class DeterminismServiceUniformTests
    {
        [Test]
        public void Decide_SameInputsRepeated_ProducesEqualResults()
        {
            // Arrange
            DeterminismService service = new();
            DecisionRequest request = BuildRequest(SelectionMethod.UniformSelectionWithoutReplacement, 2,
                ReplacementBehavior.RemovedForThisDecision, 1);
            CandidateSnapshot snapshot = BuildSnapshot(5);

            // Act
            DecisionResult first = service.Decide(request, snapshot);
            DecisionResult second = service.Decide(request, snapshot);

            // Assert
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Decide_TwoServiceInstances_ProduceEqualResults()
        {
            DecisionRequest request = BuildRequest(SelectionMethod.UniformSelectionWithoutReplacement, 3,
                ReplacementBehavior.RemovedForThisDecision, 1);
            CandidateSnapshot snapshot = BuildSnapshot(5);

            DecisionResult first = new DeterminismService().Decide(request, snapshot);
            DecisionResult second = new DeterminismService().Decide(request, snapshot);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Decide_UniformSingle_SelectsExactlyOneKnownAnswer()
        {
            // Ordinal 2 selects canonical index 0 (c0); ordinal 3 selects canonical index 1 (c1).
            DeterminismService service = new();
            CandidateSnapshot snapshot = BuildSnapshot(5);

            DecisionResult ordinalTwo = service.Decide(
                BuildRequest(SelectionMethod.UniformSingleSelection, 1, ReplacementBehavior.RemainsEligible, 2), snapshot);
            DecisionResult ordinalThree = service.Decide(
                BuildRequest(SelectionMethod.UniformSingleSelection, 1, ReplacementBehavior.RemainsEligible, 3), snapshot);

            Assert.That(ordinalTwo.Outcome.SelectedIdentities, Is.EqualTo(new[] { "c0" }));
            Assert.That(ordinalThree.Outcome.SelectedIdentities, Is.EqualTo(new[] { "c1" }));
            Assert.That(ordinalThree.Outcome, Is.Not.EqualTo(ordinalTwo.Outcome));
        }

        [Test]
        public void Decide_UniformWithoutReplacement_ThreeOfFive_KnownAnswerAndShrinkingPopulations()
        {
            // Arrange
            DeterminismService service = new();
            DecisionRequest request = BuildRequest(SelectionMethod.UniformSelectionWithoutReplacement, 3,
                ReplacementBehavior.RemovedForThisDecision, 1);
            CandidateSnapshot snapshot = BuildSnapshot(5);

            // Act
            DecisionResult result = service.Decide(request, snapshot);

            // Assert — three distinct identities in the pinned order.
            Assert.That(result.Outcome.SelectedIdentities, Is.EqualTo(new[] { "c0", "c2", "c4" }));
            Assert.That(result.Outcome.Disposition, Is.EqualTo(DecisionDisposition.Selected));
            Assert.That(result.Record.Draws, Has.Count.EqualTo(3));

            Assert.That(result.Record.Draws[0].DrawOrdinal, Is.EqualTo(1));
            Assert.That(result.Record.Draws[0].PopulationBefore, Is.EqualTo(new[] { "c0", "c1", "c2", "c3", "c4" }));
            Assert.That(result.Record.Draws[0].SelectedIdentity, Is.EqualTo("c0"));
            Assert.That(result.Record.Draws[0].Consequence, Is.EqualTo(ReplacementBehavior.RemovedForThisDecision));
            Assert.That(result.Record.Draws[0].PopulationAfter, Is.EqualTo(new[] { "c1", "c2", "c3", "c4" }));

            Assert.That(result.Record.Draws[1].DrawOrdinal, Is.EqualTo(2));
            Assert.That(result.Record.Draws[1].PopulationBefore, Is.EqualTo(new[] { "c1", "c2", "c3", "c4" }));
            Assert.That(result.Record.Draws[1].SelectedIdentity, Is.EqualTo("c2"));
            Assert.That(result.Record.Draws[1].PopulationAfter, Is.EqualTo(new[] { "c1", "c3", "c4" }));

            Assert.That(result.Record.Draws[2].DrawOrdinal, Is.EqualTo(3));
            Assert.That(result.Record.Draws[2].SelectedIdentity, Is.EqualTo("c4"));
            Assert.That(result.Record.Draws[2].PopulationAfter, Is.EqualTo(new[] { "c1", "c3" }));

            Assert.That(result.Record.ResultPermutation, Is.Empty);
            Assert.That(result.Record.RevisionIdentity, Is.EqualTo("iterate-rng-1"));
        }

        [Test]
        public void Decide_UniformWithReplacement_SingleCandidateCountThree_RepeatsSameIdentity()
        {
            // With replacement, a count exceeding the population is legal (CAB-EVT-816) — never insufficiency.
            DeterminismService service = new();
            DecisionRequest request = BuildRequest(SelectionMethod.UniformSelectionWithReplacement, 3,
                ReplacementBehavior.RemainsEligible, 1);
            CandidateSnapshot snapshot = BuildSnapshot(1);

            DecisionResult result = service.Decide(request, snapshot);

            Assert.That(result.Outcome.SelectedIdentities, Is.EqualTo(new[] { "c0", "c0", "c0" }));
            Assert.That(result.Outcome.Disposition, Is.EqualTo(DecisionDisposition.Selected));
            Assert.That(result.Record.Draws, Has.Count.EqualTo(3));
            Assert.That(result.Record.Draws[0].PopulationAfter, Is.EqualTo(new[] { "c0" }));
        }

        [Test]
        public void Decide_TwoDrawSuccess_PinsEventSequence()
        {
            DeterminismService service = new();
            DecisionRequest request = BuildRequest(SelectionMethod.UniformSelectionWithoutReplacement, 2,
                ReplacementBehavior.RemovedForThisDecision, 1);
            CandidateSnapshot snapshot = BuildSnapshot(3);

            DecisionResult result = service.Decide(request, snapshot);

            RandomSelectionEventKind[] kinds = KindsOf(result.Events);
            Assert.That(kinds, Is.EqualTo(new[]
            {
                RandomSelectionEventKind.DecisionCreated,
                RandomSelectionEventKind.CandidateSnapshotCaptured,
                RandomSelectionEventKind.DrawResolved,
                RandomSelectionEventKind.DrawResolved,
                RandomSelectionEventKind.SelectionCompleted
            }));
        }

        [Test]
        public void Decide_WeightedMethodWithUnweightedSnapshot_Throws()
        {
            DeterminismService service = new();
            DecisionRequest weighted = new(
                "sel-1", "Random target selection", "boundary", "source", "eligibility", "at capture",
                "canonical", 1, SelectionMethod.WeightedSelection, ReplacementBehavior.RemainsEligible,
                "by weight", "tiebreak", null, InsufficientCandidateBehavior.FailToQualify, BuildContext(1));

            Assert.Throws<System.ArgumentException>(() => service.Decide(weighted, BuildSnapshot(3)));
        }

        [Test]
        public void Decide_NonWeightedMethodWithWeightedSnapshot_Throws()
        {
            DeterminismService service = new();
            DecisionRequest uniform = BuildRequest(SelectionMethod.UniformSingleSelection, 1,
                ReplacementBehavior.RemainsEligible, 1);

            Assert.Throws<System.ArgumentException>(() => service.Decide(uniform, BuildWeightedSnapshot(3)));
        }

        [Test]
        public void Decide_TooSmallSnapshot_NeverThrows()
        {
            // Population shortfall is a legal runtime fact routed through insufficiency, never an exception.
            DeterminismService service = new();
            DecisionRequest request = BuildRequest(SelectionMethod.UniformSelectionWithoutReplacement, 3,
                ReplacementBehavior.RemovedForThisDecision, 1, InsufficientCandidateBehavior.FailToQualify);

            Assert.DoesNotThrow(() => service.Decide(request, BuildSnapshot(2)));
        }

        private static CandidateSnapshot BuildSnapshot(int count)
        {
            List<CandidateEntry> entries = new();
            for (int index = 0; index < count; index++)
            {
                string identity = "c" + index;
                entries.Add(new CandidateEntry(identity, new CandidateOrderingKey(identity, null, null, null), null));
            }

            return CandidateSnapshot.Create(entries);
        }

        private static CandidateSnapshot BuildWeightedSnapshot(int count)
        {
            List<CandidateEntry> entries = new();
            for (int index = 0; index < count; index++)
            {
                string identity = "c" + index;
                entries.Add(new CandidateEntry(identity, new CandidateOrderingKey(identity, null, null, null), index + 1));
            }

            return CandidateSnapshot.Create(entries);
        }

        private static DecisionContext BuildContext(int ordinal)
        {
            return DecisionContext.Derive(new DecisionContextComponents(
                "seed", null, null, null, null, null, null, null, "Random target selection", ordinal));
        }

        private static DecisionRequest BuildRequest(
            SelectionMethod method,
            int count,
            ReplacementBehavior replacement,
            int ordinal,
            InsufficientCandidateBehavior insufficient = InsufficientCandidateBehavior.FailToQualify)
        {
            return new DecisionRequest(
                "sel-1",
                "Random target selection",
                "boundary",
                "source",
                "eligibility",
                "at capture",
                "canonical",
                count,
                method,
                replacement,
                null,
                "tiebreak",
                null,
                insufficient,
                BuildContext(ordinal));
        }

        private static RandomSelectionEventKind[] KindsOf(IReadOnlyList<RandomSelectionEvent> events)
        {
            RandomSelectionEventKind[] kinds = new RandomSelectionEventKind[events.Count];
            for (int index = 0; index < events.Count; index++)
                kinds[index] = events[index].Kind;

            return kinds;
        }
    }
}
