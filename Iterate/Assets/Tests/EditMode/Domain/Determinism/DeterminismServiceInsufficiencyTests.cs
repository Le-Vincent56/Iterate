using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Determinism.Tests
{
    /// <summary>
    /// Tests the pipeline's insufficiency routing: a population too small
    /// for the declared selection count resolves through the declared
    /// <see cref="InsufficientCandidateBehavior"/> — never an exception. Each case uses a 2-candidate
    /// snapshot with an uniform-without-replacement request for count 3 (three distinct needed, two
    /// available). Draw selections are reproduced from the reference algorithm chain.
    /// </summary>
    public sealed class DeterminismServiceInsufficiencyTests
    {
        [Test]
        public void Decide_FailToQualify_EmptyOutcomeAndFailedEvent()
        {
            DecisionResult result = Decide(InsufficientCandidateBehavior.FailToQualify);

            Assert.That(result.Outcome.Disposition, Is.EqualTo(DecisionDisposition.FailedToQualify));
            Assert.That(result.Outcome.SelectedIdentities, Is.Empty);
            Assert.That(result.Record.Draws, Is.Empty);
            Assert.That(TerminalKind(result), Is.EqualTo(RandomSelectionEventKind.SelectionFailed));
        }

        [Test]
        public void Decide_CancelTheDecision_EmptyOutcomeAndFailedEvent()
        {
            DecisionResult result = Decide(InsufficientCandidateBehavior.CancelTheDecision);

            Assert.That(result.Outcome.Disposition, Is.EqualTo(DecisionDisposition.Cancelled));
            Assert.That(result.Outcome.SelectedIdentities, Is.Empty);
            Assert.That(result.Record.Draws, Is.Empty);
            Assert.That(TerminalKind(result), Is.EqualTo(RandomSelectionEventKind.SelectionFailed));
        }

        [Test]
        public void Decide_UseDeclaredFallback_EmptyOutcomeAndFallbackEvent()
        {
            DecisionResult result = Decide(InsufficientCandidateBehavior.UseDeclaredFallback);

            Assert.That(result.Outcome.Disposition, Is.EqualTo(DecisionDisposition.FallbackResolved));
            Assert.That(result.Outcome.SelectedIdentities, Is.Empty);
            Assert.That(result.Record.Draws, Is.Empty);
            Assert.That(TerminalKind(result), Is.EqualTo(RandomSelectionEventKind.FallbackResolved));
        }

        [Test]
        public void Decide_SelectAllRemaining_SelectsBothInCanonicalOrder()
        {
            DecisionResult result = Decide(InsufficientCandidateBehavior.SelectAllRemainingCandidates);

            Assert.That(result.Outcome.Disposition, Is.EqualTo(DecisionDisposition.SelectedAllRemaining));
            Assert.That(result.Outcome.SelectedIdentities, Is.EqualTo(new[] { "c0", "c1" }));
            Assert.That(result.Record.Draws, Has.Count.EqualTo(2));
            Assert.That(result.Record.Draws[0].SelectedIdentity, Is.EqualTo("c0"));
            Assert.That(result.Record.Draws[1].SelectedIdentity, Is.EqualTo("c1"));
            Assert.That(TerminalKind(result), Is.EqualTo(RandomSelectionEventKind.SelectionCompleted));
        }

        [Test]
        public void Decide_ReduceSelectionCount_DrawsAvailablePopulation()
        {
            DecisionResult result = Decide(InsufficientCandidateBehavior.ReduceSelectionCount);

            Assert.That(result.Outcome.Disposition, Is.EqualTo(DecisionDisposition.ReducedCount));
            // Reduced to the two available; the without-replacement draw order under ordinal 1 is c1, c0.
            Assert.That(result.Outcome.SelectedIdentities, Is.EqualTo(new[] { "c1", "c0" }));
            Assert.That(result.Record.Draws, Has.Count.EqualTo(2));
            Assert.That(TerminalKind(result), Is.EqualTo(RandomSelectionEventKind.SelectionCompleted));
        }

        [Test]
        public void Decide_AnyInsufficiencyBehavior_NeverThrows(
            [Values(InsufficientCandidateBehavior.FailToQualify, InsufficientCandidateBehavior.CancelTheDecision,
                InsufficientCandidateBehavior.UseDeclaredFallback,
                InsufficientCandidateBehavior.SelectAllRemainingCandidates,
                InsufficientCandidateBehavior.ReduceSelectionCount)] InsufficientCandidateBehavior behavior)
        {
            Assert.DoesNotThrow(() => Decide(behavior));
        }

        private static DecisionResult Decide(InsufficientCandidateBehavior behavior)
        {
            DeterminismService service = new();
            DecisionContext context = DecisionContext.Derive(new DecisionContextComponents(
                "seed", null, null, null, null, null, null, null, "Random target selection", 1));
            DecisionRequest request = new(
                "sel-1",
                "Random target selection",
                "boundary",
                "source",
                "eligibility",
                "at capture",
                "canonical",
                3,
                SelectionMethod.UniformSelectionWithoutReplacement,
                ReplacementBehavior.RemovedForThisDecision,
                null,
                "tiebreak",
                null,
                behavior,
                context);

            return service.Decide(request, BuildSnapshot(2));
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

        private static RandomSelectionEventKind TerminalKind(DecisionResult result)
        {
            return result.Events[result.Events.Count - 1].Kind;
        }
    }
}
