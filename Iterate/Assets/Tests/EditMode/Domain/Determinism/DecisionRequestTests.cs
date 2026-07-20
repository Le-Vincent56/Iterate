using System;
using NUnit.Framework;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Determinism.Tests
{
    /// <summary>
    /// Construction-validation tests for <see cref="DecisionRequest"/> — the CAB-EVT-782 declaration
    /// enforced by construction. Covers field round-trip, always-required non-empty strings, the count
    /// rule (shuffle = 0 sentinel, others ≥ 1 — design-review required change 1), the method × replacement
    /// consistency table (required change 2), weighting-rule and suffix-disposition conditionality,
    /// registry-checked purpose, purpose/context agreement, and null context. A defaulted helper builds a
    /// valid baseline so each negative test varies exactly one field.
    /// </summary>
    public sealed class DecisionRequestTests
    {
        private const string DefaultPurpose = "Random target selection";

        [Test]
        public void Constructor_ValidWeightedRequest_RoundTripsAllFields()
        {
            // Arrange
            DecisionContext context = BuildContext(DefaultPurpose);

            // Act
            DecisionRequest request = new(
                "sel-1",
                DefaultPurpose,
                "boundary",
                "source",
                "eligibility",
                "at capture",
                "canonical",
                2,
                SelectionMethod.WeightedSelection,
                ReplacementBehavior.RemainsEligible,
                "by weight",
                "tiebreak",
                null,
                InsufficientCandidateBehavior.FailToQualify,
                context);

            // Assert
            Assert.That(request.SelectionIdentity, Is.EqualTo("sel-1"));
            Assert.That(request.SelectionPurpose, Is.EqualTo(DefaultPurpose));
            Assert.That(request.SelectionBoundary, Is.EqualTo("boundary"));
            Assert.That(request.CandidateSource, Is.EqualTo("source"));
            Assert.That(request.EligibilityRule, Is.EqualTo("eligibility"));
            Assert.That(request.CandidateSnapshotTiming, Is.EqualTo("at capture"));
            Assert.That(request.CandidateOrderingRule, Is.EqualTo("canonical"));
            Assert.That(request.SelectionCount, Is.EqualTo(2));
            Assert.That(request.Method, Is.EqualTo(SelectionMethod.WeightedSelection));
            Assert.That(request.Replacement, Is.EqualTo(ReplacementBehavior.RemainsEligible));
            Assert.That(request.WeightingRule, Is.EqualTo("by weight"));
            Assert.That(request.TieBreakRule, Is.EqualTo("tiebreak"));
            Assert.That(request.UnconsumedSuffixDisposition, Is.Null);
            Assert.That(request.InsufficientCandidates, Is.EqualTo(InsufficientCandidateBehavior.FailToQualify));
            Assert.That(request.Context, Is.EqualTo(context));
        }

        [Test]
        public void Constructor_OrderingRequest_RoundTripsSuffixDisposition()
        {
            DecisionRequest request = BuildRequest(
                method: SelectionMethod.RandomOrderingOfCapturedFiniteSet,
                selectionCount: 2,
                replacement: ReplacementBehavior.RemainsEligible,
                unconsumedSuffixDisposition: "discard the remainder");

            Assert.That(request.UnconsumedSuffixDisposition, Is.EqualTo("discard the remainder"));
        }

        [Test]
        public void Constructor_EmptyAlwaysRequiredString_Throws(
            [Values("selectionIdentity", "selectionBoundary", "candidateSource", "eligibilityRule",
                "candidateSnapshotTiming", "candidateOrderingRule", "tieBreakRule")] string field)
        {
            Assert.Throws<ArgumentException>(() => BuildRequest(
                selectionIdentity: field == "selectionIdentity" ? "" : "sel-1",
                selectionBoundary: field == "selectionBoundary" ? "" : "boundary",
                candidateSource: field == "candidateSource" ? "" : "source",
                eligibilityRule: field == "eligibilityRule" ? "" : "eligibility",
                candidateSnapshotTiming: field == "candidateSnapshotTiming" ? "" : "at capture",
                candidateOrderingRule: field == "candidateOrderingRule" ? "" : "canonical",
                tieBreakRule: field == "tieBreakRule" ? "" : "tiebreak"));
        }

        [Test]
        public void Constructor_NullSelectionIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => BuildRequest(selectionIdentity: null));
        }

        [Test]
        public void Constructor_SelectionMethodWithZeroCount_Throws()
        {
            Assert.Throws<ArgumentException>(() => BuildRequest(
                method: SelectionMethod.UniformSingleSelection,
                selectionCount: 0));
        }

        [Test]
        public void Constructor_ShuffleWithZeroCount_Accepted()
        {
            Assert.DoesNotThrow(() => BuildRequest(
                method: SelectionMethod.DeterministicShuffle,
                selectionCount: 0,
                replacement: ReplacementBehavior.RemainsEligible));
        }

        [Test]
        public void Constructor_ShuffleWithNonzeroCount_Throws()
        {
            Assert.Throws<ArgumentException>(() => BuildRequest(
                method: SelectionMethod.DeterministicShuffle,
                selectionCount: 1,
                replacement: ReplacementBehavior.RemainsEligible));
        }

        [Test]
        public void Constructor_UnknownPurpose_Throws()
        {
            Assert.Throws<ArgumentException>(() => BuildRequest(selectionPurpose: "Nonexistent purpose"));
        }

        [Test]
        public void Constructor_PurposeContextMismatch_Throws()
        {
            // Request purpose is valid but disagrees with the context's own selection purpose.
            Assert.Throws<ArgumentException>(() => BuildRequest(
                selectionPurpose: "Random target selection",
                context: BuildContext("Random ordering")));
        }

        [Test]
        public void Constructor_WithoutReplacementRemovalBehaviors_Accepted(
            [Values(ReplacementBehavior.RemovedForThisDecision,
                ReplacementBehavior.RemovedFromLongerLivedPopulation)] ReplacementBehavior replacement)
        {
            Assert.DoesNotThrow(() => BuildRequest(
                method: SelectionMethod.UniformSelectionWithoutReplacement,
                replacement: replacement));
        }

        [Test]
        public void Constructor_WithoutReplacementNonRemovalBehaviors_Throws(
            [Values(ReplacementBehavior.RemainsEligible,
                ReplacementBehavior.TransformedBeforeLaterDraws)] ReplacementBehavior replacement)
        {
            Assert.Throws<ArgumentException>(() => BuildRequest(
                method: SelectionMethod.UniformSelectionWithoutReplacement,
                replacement: replacement));
        }

        [Test]
        public void Constructor_WithReplacementRemainsEligible_Accepted()
        {
            Assert.DoesNotThrow(() => BuildRequest(
                method: SelectionMethod.UniformSelectionWithReplacement,
                replacement: ReplacementBehavior.RemainsEligible));
        }

        [Test]
        public void Constructor_WithReplacementNonEligible_Throws()
        {
            Assert.Throws<ArgumentException>(() => BuildRequest(
                method: SelectionMethod.UniformSelectionWithReplacement,
                replacement: ReplacementBehavior.RemovedForThisDecision));
        }

        [Test]
        public void Constructor_ShuffleNonEligible_Throws()
        {
            Assert.Throws<ArgumentException>(() => BuildRequest(
                method: SelectionMethod.DeterministicShuffle,
                selectionCount: 0,
                replacement: ReplacementBehavior.RemovedForThisDecision));
        }

        [Test]
        public void Constructor_OrderingNonEligible_Throws()
        {
            Assert.Throws<ArgumentException>(() => BuildRequest(
                method: SelectionMethod.RandomOrderingOfCapturedFiniteSet,
                replacement: ReplacementBehavior.TransformedBeforeLaterDraws,
                unconsumedSuffixDisposition: "discard the remainder"));
        }

        [Test]
        public void Constructor_UniformSingleAnyReplacement_Accepted(
            [Values(ReplacementBehavior.RemainsEligible, ReplacementBehavior.RemovedForThisDecision,
                ReplacementBehavior.RemovedFromLongerLivedPopulation,
                ReplacementBehavior.TransformedBeforeLaterDraws)] ReplacementBehavior replacement)
        {
            Assert.DoesNotThrow(() => BuildRequest(
                method: SelectionMethod.UniformSingleSelection,
                replacement: replacement));
        }

        [Test]
        public void Constructor_WeightedAnyReplacement_Accepted(
            [Values(ReplacementBehavior.RemainsEligible,
                ReplacementBehavior.RemovedForThisDecision)] ReplacementBehavior replacement)
        {
            Assert.DoesNotThrow(() => BuildRequest(
                method: SelectionMethod.WeightedSelection,
                replacement: replacement,
                weightingRule: "by weight"));
        }

        [Test]
        public void Constructor_WeightedWithoutWeightingRule_Throws()
        {
            Assert.Throws<ArgumentException>(() => BuildRequest(
                method: SelectionMethod.WeightedSelection,
                replacement: ReplacementBehavior.RemainsEligible,
                weightingRule: null));
        }

        [Test]
        public void Constructor_NonWeightedWithWeightingRule_Throws()
        {
            Assert.Throws<ArgumentException>(() => BuildRequest(
                method: SelectionMethod.UniformSingleSelection,
                weightingRule: "by weight"));
        }

        [Test]
        public void Constructor_OrderingWithoutSuffixDisposition_Throws()
        {
            Assert.Throws<ArgumentException>(() => BuildRequest(
                method: SelectionMethod.RandomOrderingOfCapturedFiniteSet,
                replacement: ReplacementBehavior.RemainsEligible,
                unconsumedSuffixDisposition: null));
        }

        [Test]
        public void Constructor_NonOrderingWithSuffixDisposition_Throws()
        {
            Assert.Throws<ArgumentException>(() => BuildRequest(
                method: SelectionMethod.UniformSingleSelection,
                unconsumedSuffixDisposition: "discard the remainder"));
        }

        [Test]
        public void Constructor_NullContext_Throws()
        {
            // The helper substitutes a context, so construct directly to pass a genuine null.
            Assert.Throws<ArgumentException>(() => new DecisionRequest(
                "sel-1",
                DefaultPurpose,
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
                null));
        }

        private static DecisionContext BuildContext(string purpose)
        {
            return DecisionContext.Derive(new DecisionContextComponents(
                "seed", null, null, null, null, null, null, null, purpose, 1));
        }

        private static DecisionRequest BuildRequest(
            string selectionIdentity = "sel-1",
            string selectionPurpose = DefaultPurpose,
            string selectionBoundary = "boundary",
            string candidateSource = "source",
            string eligibilityRule = "eligibility",
            string candidateSnapshotTiming = "at capture",
            string candidateOrderingRule = "canonical",
            int selectionCount = 1,
            SelectionMethod method = SelectionMethod.UniformSingleSelection,
            ReplacementBehavior replacement = ReplacementBehavior.RemainsEligible,
            string weightingRule = null,
            string tieBreakRule = "tiebreak",
            string unconsumedSuffixDisposition = null,
            InsufficientCandidateBehavior insufficientCandidates = InsufficientCandidateBehavior.FailToQualify,
            DecisionContext context = null)
        {
            DecisionContext effectiveContext = context ?? BuildContext(selectionPurpose);
            return new DecisionRequest(
                selectionIdentity,
                selectionPurpose,
                selectionBoundary,
                candidateSource,
                eligibilityRule,
                candidateSnapshotTiming,
                candidateOrderingRule,
                selectionCount,
                method,
                replacement,
                weightingRule,
                tieBreakRule,
                unconsumedSuffixDisposition,
                insufficientCandidates,
                effectiveContext);
        }
    }
}
