using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Determinism.Tests
{
    /// <summary>
    /// Tests deterministic shuffle (CAB-EVT-823) and random ordering (CAB-EVT-824). Permutations were
    /// reproduced from the reference chain running Fisher–Yates over the canonical order with the
    /// per-decision PCG stream (session "seed", purpose "Random ordering", snapshot c0..c4). The shuffle is
    /// one decision with one draw record whose populations stay the captured set in canonical order; the
    /// full permutation lives in the record's first-class result permutation.
    /// </summary>
    public sealed class DeterminismServiceOrderingTests
    {
        [Test]
        public void Decide_Shuffle_ProducesPinnedPermutation()
        {
            // Arrange
            DeterminismService service = new();
            DecisionRequest request = ShuffleRequest(1);
            CandidateSnapshot snapshot = BuildSnapshot(5);

            // Act
            DecisionResult result = service.Decide(request, snapshot);

            // Assert
            Assert.That(result.Outcome.SelectedIdentities, Is.EqualTo(new[] { "c2", "c4", "c0", "c1", "c3" }));
            Assert.That(result.Record.ResultPermutation, Is.EqualTo(new[] { "c2", "c4", "c0", "c1", "c3" }));
            Assert.That(result.Record.Draws, Has.Count.EqualTo(1));
            Assert.That(result.Record.Draws[0].DrawOrdinal, Is.EqualTo(1));
            Assert.That(result.Record.Draws[0].PopulationBefore, Is.EqualTo(new[] { "c0", "c1", "c2", "c3", "c4" }));
            Assert.That(result.Record.Draws[0].PopulationAfter, Is.EqualTo(new[] { "c0", "c1", "c2", "c3", "c4" }));
            Assert.That(result.Record.Draws[0].SelectedIdentity, Is.EqualTo("c2"));
        }

        [Test]
        public void Decide_Shuffle_IsAPermutationOfTheCapturedSet()
        {
            DeterminismService service = new();
            DecisionResult result = service.Decide(ShuffleRequest(1), BuildSnapshot(5));

            Assert.That(result.Outcome.SelectedIdentities,
                Is.EquivalentTo(new[] { "c0", "c1", "c2", "c3", "c4" }));
        }

        [Test]
        public void Decide_Shuffle_DifferentOrdinals_ProduceDifferentPermutations()
        {
            DeterminismService service = new();
            CandidateSnapshot snapshot = BuildSnapshot(5);

            DecisionResult ordinalOne = service.Decide(ShuffleRequest(1), snapshot);
            DecisionResult ordinalTwo = service.Decide(ShuffleRequest(2), snapshot);

            Assert.That(ordinalTwo.Record.ResultPermutation,
                Is.Not.EqualTo(ordinalOne.Record.ResultPermutation));
        }

        [Test]
        public void Decide_Shuffle_DiffersFromRepeatedWithoutReplacementDraws()
        {
            // CAB-EVT-825: a shuffle is not repeated draws — different stream consumption under one context.
            DeterminismService service = new();
            CandidateSnapshot snapshot = BuildSnapshot(5);

            DecisionResult shuffle = service.Decide(ShuffleRequest(1), snapshot);
            DecisionResult repeatedDraws = service.Decide(
                new DecisionRequest("sel-1", "Random ordering", "boundary", "source", "eligibility",
                    "at capture", "canonical", 5, SelectionMethod.UniformSelectionWithoutReplacement,
                    ReplacementBehavior.RemovedForThisDecision, null, "tiebreak", null,
                    InsufficientCandidateBehavior.FailToQualify, BuildContext(1)),
                snapshot);

            Assert.That(shuffle.Outcome.SelectedIdentities,
                Is.Not.EqualTo(repeatedDraws.Outcome.SelectedIdentities));
        }

        [Test]
        public void Decide_RandomOrdering_TakesDeclaredPrefixAndRetainsFullPermutation()
        {
            // Arrange
            DeterminismService service = new();
            DecisionRequest request = OrderingRequest(2, 1);
            CandidateSnapshot snapshot = BuildSnapshot(5);

            // Act
            DecisionResult result = service.Decide(request, snapshot);

            // Assert — outcome is the first two of the same Fisher–Yates permutation.
            Assert.That(result.Outcome.SelectedIdentities, Is.EqualTo(new[] { "c2", "c4" }));
            Assert.That(result.Record.ResultPermutation, Is.EqualTo(new[] { "c2", "c4", "c0", "c1", "c3" }));
            Assert.That(result.Record.Draws, Has.Count.EqualTo(1));
            Assert.That(result.Record.Draws[0].PopulationBefore, Is.EqualTo(new[] { "c0", "c1", "c2", "c3", "c4" }));
        }

        [Test]
        public void Decide_ShuffleAndOrdering_AreDeterministicAcrossCalls()
        {
            DeterminismService service = new();
            CandidateSnapshot snapshot = BuildSnapshot(5);

            Assert.That(service.Decide(ShuffleRequest(1), snapshot),
                Is.EqualTo(service.Decide(ShuffleRequest(1), snapshot)));
            Assert.That(service.Decide(OrderingRequest(2, 1), snapshot),
                Is.EqualTo(service.Decide(OrderingRequest(2, 1), snapshot)));
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

        private static DecisionContext BuildContext(int ordinal)
        {
            return DecisionContext.Derive(new DecisionContextComponents(
                "seed", null, null, null, null, null, null, null, "Random ordering", ordinal));
        }

        private static DecisionRequest ShuffleRequest(int ordinal)
        {
            return new DecisionRequest(
                "sel-1", "Random ordering", "boundary", "source", "eligibility", "at capture", "canonical",
                0, SelectionMethod.DeterministicShuffle, ReplacementBehavior.RemainsEligible, null,
                "tiebreak", null, InsufficientCandidateBehavior.FailToQualify, BuildContext(ordinal));
        }

        private static DecisionRequest OrderingRequest(int count, int ordinal)
        {
            return new DecisionRequest(
                "sel-1", "Random ordering", "boundary", "source", "eligibility", "at capture", "canonical",
                count, SelectionMethod.RandomOrderingOfCapturedFiniteSet, ReplacementBehavior.RemainsEligible,
                null, "tiebreak", "discard the remainder", InsufficientCandidateBehavior.FailToQualify,
                BuildContext(ordinal));
        }
    }
}
