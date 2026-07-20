using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Determinism.Tests
{
    /// <summary>
    /// The TA-RND-006 determinism conformance sweep over the assembled service — the Feature's
    /// Definition-of-Done gate. Covers context isolation (CAB-EVT-848), end-to-end ordering-permutation
    /// invariance (CAB-EVT-812), replay by recomputation (CAB-EVT-905), retry seed reuse (TA-RND-005), and
    /// revision-identity stamping (CAB-EVT-763). Assertions are property-based (value-equal records, stable
    /// selected positions), so they hold against the reference algorithm without pinning raw draws.
    /// </summary>
    public sealed class DeterminismConformanceTests
    {
        [Test]
        public void ContextIsolation_UnrelatedInterleavedDecisions_DoNotPerturbTheDecisionUnderTest()
        {
            // Arrange
            DeterminismService service = new();
            CandidateSnapshot snapshot = BuildSnapshot(new[] { "c0", "c1", "c2", "c3", "c4" });

            OccurrenceOrdinalTracker aloneTracker = new();
            int ordinalAlone = aloneTracker.Next("Random ordering", "scope");
            DecisionResult resultAlone = service.Decide(
                BuildRequest(SelectionMethod.UniformSelectionWithoutReplacement, 3,
                    ReplacementBehavior.RemovedForThisDecision, ordinalAlone), snapshot);

            OccurrenceOrdinalTracker interleavedTracker = new();
            interleavedTracker.Next("Reward generation", "scope");       // unrelated decision before
            int ordinalUnderTest = interleavedTracker.Next("Random ordering", "scope");
            interleavedTracker.Next("Basic Indexing", "scope");          // unrelated decision after
            DecisionResult resultInterleaved = service.Decide(
                BuildRequest(SelectionMethod.UniformSelectionWithoutReplacement, 3,
                    ReplacementBehavior.RemovedForThisDecision, ordinalUnderTest), snapshot);

            // Assert — unrelated purposes never touch the decision-under-test's ordinal or record.
            Assert.That(ordinalUnderTest, Is.EqualTo(ordinalAlone));
            Assert.That(resultInterleaved, Is.EqualTo(resultAlone));
        }

        [Test]
        public void OrderingPermutations_ShuffledInputOrders_ProduceIdenticalRecords()
        {
            // Arrange
            DeterminismService service = new();
            DecisionRequest request = BuildRequest(SelectionMethod.UniformSelectionWithoutReplacement, 3,
                ReplacementBehavior.RemovedForThisDecision, 1);
            CandidateEntry a = Entry("c0");
            CandidateEntry b = Entry("c1");
            CandidateEntry c = Entry("c2");
            CandidateEntry d = Entry("c3");
            CandidateEntry e = Entry("c4");
            CandidateEntry[][] permutations =
            {
                new[] { a, b, c, d, e },
                new[] { e, d, c, b, a },
                new[] { c, a, e, b, d },
                new[] { b, e, a, d, c }
            };

            // Act
            DecisionResult baseline = service.Decide(request, CandidateSnapshot.Create(permutations[0]));

            // Assert
            for (int index = 1; index < permutations.Length; index++)
            {
                DecisionResult result = service.Decide(request, CandidateSnapshot.Create(permutations[index]));
                Assert.That(result, Is.EqualTo(baseline), $"permutation {index}");
            }
        }

        [Test]
        public void ReplayByRecomputation_RebuiltFromEvidence_YieldsEqualRecord()
        {
            // Arrange
            DeterminismService service = new();
            DecisionResult original = service.Decide(
                BuildRequest(SelectionMethod.UniformSelectionWithoutReplacement, 3,
                    ReplacementBehavior.RemovedForThisDecision, 1),
                BuildSnapshot(new[] { "c0", "c1", "c2", "c3", "c4" }));

            // Act — rebuild the request and snapshot from the stored record's evidence and re-decide.
            DecisionRequest replayRequest = original.Record.Request;
            CandidateSnapshot replaySnapshot = CandidateSnapshot.Create(original.Record.CandidateEvidence);
            DecisionResult replay = service.Decide(replayRequest, replaySnapshot);

            // Assert — recomputation, not stored-outcome trust.
            Assert.That(replay.Record, Is.EqualTo(original.Record));
        }

        [Test]
        public void RetrySeedReuse_UnrelatedDecisionsBetweenAttempts_ReproduceTheResult()
        {
            DeterminismService service = new();
            DecisionRequest request = BuildRequest(SelectionMethod.UniformSelectionWithoutReplacement, 3,
                ReplacementBehavior.RemovedForThisDecision, 1);
            CandidateSnapshot snapshot = BuildSnapshot(new[] { "c0", "c1", "c2", "c3", "c4" });

            DecisionResult firstAttempt = service.Decide(request, snapshot);
            service.Decide(
                BuildRequest(SelectionMethod.UniformSingleSelection, 1, ReplacementBehavior.RemainsEligible, 7),
                BuildSnapshot(new[] { "x0", "x1" }));   // unrelated decision between attempts
            DecisionResult secondAttempt = service.Decide(request, snapshot);

            Assert.That(secondAttempt, Is.EqualTo(firstAttempt));
        }

        [Test]
        public void RetrySeedReuse_CandidateSwappedUnderSameContext_KeepsSelectedPosition()
        {
            // A candidate swapped for a new identity but keeping its ordering key stays in the same canonical
            // position, and the same context applies the same draw position.
            DeterminismService service = new();
            DecisionRequest request = BuildRequest(SelectionMethod.UniformSingleSelection, 1,
                ReplacementBehavior.RemainsEligible, 2);

            CandidateSnapshot original = BuildSnapshot(new[] { "c0", "c1", "c2", "c3", "c4" });
            CandidateSnapshot swapped = BuildSnapshotWithKeys(
                new[] { "swapped0", "c1", "c2", "c3", "c4" },
                new[] { "c0", "c1", "c2", "c3", "c4" });

            DecisionResult originalResult = service.Decide(request, original);
            DecisionResult swappedResult = service.Decide(request, swapped);

            int originalPosition = PositionOf(original, originalResult.Outcome.SelectedIdentities[0]);
            int swappedPosition = PositionOf(swapped, swappedResult.Outcome.SelectedIdentities[0]);
            Assert.That(swappedPosition, Is.EqualTo(originalPosition));
        }

        [Test]
        public void RevisionIdentity_EveryMethod_CarriesIterateRng1()
        {
            DeterminismService service = new();
            CandidateSnapshot unweighted = BuildSnapshot(new[] { "c0", "c1", "c2", "c3", "c4" });
            CandidateSnapshot weighted = BuildWeightedSnapshot(
                new[] { "c0", "c1", "c2" }, new[] { 5, 2, 3 });

            DecisionResult[] results =
            {
                service.Decide(BuildRequest(SelectionMethod.UniformSingleSelection, 1, ReplacementBehavior.RemainsEligible, 1), unweighted),
                service.Decide(BuildRequest(SelectionMethod.UniformSelectionWithoutReplacement, 2, ReplacementBehavior.RemovedForThisDecision, 1), unweighted),
                service.Decide(BuildRequest(SelectionMethod.UniformSelectionWithReplacement, 2, ReplacementBehavior.RemainsEligible, 1), unweighted),
                service.Decide(BuildRequest(SelectionMethod.WeightedSelection, 1, ReplacementBehavior.RemainsEligible, 1, "by weight"), weighted),
                service.Decide(BuildRequest(SelectionMethod.DeterministicShuffle, 0, ReplacementBehavior.RemainsEligible, 1), unweighted),
                service.Decide(BuildRequest(SelectionMethod.RandomOrderingOfCapturedFiniteSet, 2, ReplacementBehavior.RemainsEligible, 1, null, "discard the remainder"), unweighted)
            };

            for (int index = 0; index < results.Length; index++)
            {
                Assert.That(results[index].Record.RevisionIdentity, Is.EqualTo("iterate-rng-1"), $"method {index}");
                Assert.That(results[index].Record.RevisionIdentity, Is.EqualTo(DeterminismService.RevisionIdentity));
            }
        }

        private static CandidateEntry Entry(string identity)
        {
            return new CandidateEntry(identity, new CandidateOrderingKey(identity, null, null, null), null);
        }

        private static CandidateSnapshot BuildSnapshot(string[] identities)
        {
            List<CandidateEntry> entries = new();
            for (int index = 0; index < identities.Length; index++)
                entries.Add(Entry(identities[index]));

            return CandidateSnapshot.Create(entries);
        }

        private static CandidateSnapshot BuildSnapshotWithKeys(string[] identities, string[] definitionKeys)
        {
            List<CandidateEntry> entries = new();
            for (int index = 0; index < identities.Length; index++)
            {
                entries.Add(new CandidateEntry(
                    identities[index],
                    new CandidateOrderingKey(definitionKeys[index], null, null, null),
                    null));
            }

            return CandidateSnapshot.Create(entries);
        }

        private static CandidateSnapshot BuildWeightedSnapshot(string[] identities, int[] weights)
        {
            List<CandidateEntry> entries = new();
            for (int index = 0; index < identities.Length; index++)
            {
                entries.Add(new CandidateEntry(
                    identities[index],
                    new CandidateOrderingKey(identities[index], null, null, null),
                    weights[index]));
            }

            return CandidateSnapshot.Create(entries);
        }

        private static DecisionContext BuildContext(int ordinal)
        {
            return DecisionContext.Derive(new DecisionContextComponents(
                "seed", null, null, null, null, "scope", null, null, "Random ordering", ordinal));
        }

        private static DecisionRequest BuildRequest(
            SelectionMethod method,
            int count,
            ReplacementBehavior replacement,
            int ordinal,
            string weightingRule = null,
            string suffix = null)
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
                method,
                replacement,
                weightingRule,
                "tiebreak",
                suffix,
                InsufficientCandidateBehavior.FailToQualify,
                BuildContext(ordinal));
        }

        private static int PositionOf(CandidateSnapshot snapshot, string identity)
        {
            for (int index = 0; index < snapshot.Count; index++)
            {
                if (string.Equals(snapshot.Candidates[index].Identity, identity, System.StringComparison.Ordinal))
                    return index;
            }

            return -1;
        }
    }
}
