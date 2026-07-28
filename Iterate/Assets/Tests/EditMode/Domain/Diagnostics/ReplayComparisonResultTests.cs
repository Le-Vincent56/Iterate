using System;
using System.Collections.Generic;
using Iterate.Domain.Diagnostics;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;
using NUnit.Framework;

namespace Iterate.Domain.Diagnostics.Tests
{
    /// <summary>
    /// Contract tests for <see cref="ReplayComparisonResult"/>: each status carries exactly one detail
    /// branch, and the recomputed record is present exactly when correspondence held. An
    /// exact-reproduction-unavailable result withholds the recomputed record even though the engine ran,
    /// which is what makes the silent substitution CAB-EVT-770 forbids structurally impossible for any
    /// downstream consumer.
    /// </summary>
    public sealed class ReplayComparisonResultTests
    {
        [Test]
        public void Match_CarriesTheRecomputedRecordAndNoDetailBranch()
        {
            // Arrange
            ExecutionRecord recomputed = MinimalRecord();

            // Act
            ReplayComparisonResult result = new(ReplayComparisonStatus.Match, null, null, recomputed);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayComparisonStatus.Match));
            Assert.That(result.RecomputedRecord, Is.SameAs(recomputed));
            Assert.That(result.Divergence, Is.Null);
            Assert.That(result.UnavailableComponent, Is.Null);
        }

        [Test]
        public void Diverged_CarriesTheDivergenceAndTheRecomputedRecord()
        {
            // Arrange — correspondence held, so both sides of the locus are renderable: the stored record
            // is the caller's, the recomputed one comes back on the result.
            ExecutionRecord recomputed = MinimalRecord();
            ReplayDivergence divergence = AnyDivergence();

            // Act
            ReplayComparisonResult result = new(ReplayComparisonStatus.Diverged, null, divergence, recomputed);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayComparisonStatus.Diverged));
            Assert.That(result.Divergence, Is.SameAs(divergence));
            Assert.That(result.RecomputedRecord, Is.SameAs(recomputed));
            Assert.That(result.UnavailableComponent, Is.Null);
        }

        [Test]
        public void ExactReproductionUnavailable_NamesTheComponentAndWithholdsTheRecomputedRecord()
        {
            // Act — the engine did run, but under inputs that do not correspond to the stored ones, so
            // the record it produced is not evidence about the stored execution and is not handed back.
            ReplayComparisonResult result = new(
                ReplayComparisonStatus.ExactReproductionUnavailable,
                "REVISION_STAMP:content",
                null,
                null);

            // Assert
            Assert.That(result.Status, Is.EqualTo(ReplayComparisonStatus.ExactReproductionUnavailable));
            Assert.That(result.UnavailableComponent, Is.EqualTo("REVISION_STAMP:content"));
            Assert.That(result.RecomputedRecord, Is.Null);
            Assert.That(result.Divergence, Is.Null);
        }

        [Test]
        public void ExactReproductionUnavailable_WithRecomputedRecord_Throws()
        {
            // A result that both reports non-correspondence and hands back the record computed under the
            // non-corresponding inputs is exactly the substitution CAB-EVT-770 forbids.
            Assert.Throws<ArgumentException>(() => _ = new ReplayComparisonResult(
                ReplayComparisonStatus.ExactReproductionUnavailable,
                "IDENTITY:SessionSeedIdentity",
                null,
                MinimalRecord()));
        }

        [Test]
        public void ExactReproductionUnavailable_WithoutComponent_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ReplayComparisonResult(
                ReplayComparisonStatus.ExactReproductionUnavailable,
                null,
                null,
                null));
        }

        [Test]
        public void ExactReproductionUnavailable_WithEmptyComponent_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ReplayComparisonResult(
                ReplayComparisonStatus.ExactReproductionUnavailable,
                "",
                null,
                null));
        }

        [Test]
        public void ExactReproductionUnavailable_WithDivergence_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ReplayComparisonResult(
                ReplayComparisonStatus.ExactReproductionUnavailable,
                "INITIAL_STATE",
                AnyDivergence(),
                null));
        }

        [Test]
        public void Match_WithDivergence_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ReplayComparisonResult(
                ReplayComparisonStatus.Match,
                null,
                AnyDivergence(),
                MinimalRecord()));
        }

        [Test]
        public void Match_WithUnavailableComponent_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ReplayComparisonResult(
                ReplayComparisonStatus.Match,
                "RELEVANT_PATCHES",
                null,
                MinimalRecord()));
        }

        [Test]
        public void Match_WithoutRecomputedRecord_Throws()
        {
            // Correspondence held for a match by definition, so the record is always available.
            Assert.Throws<ArgumentException>(() => _ = new ReplayComparisonResult(
                ReplayComparisonStatus.Match,
                null,
                null,
                null));
        }

        [Test]
        public void Diverged_WithoutDivergence_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ReplayComparisonResult(
                ReplayComparisonStatus.Diverged,
                null,
                null,
                MinimalRecord()));
        }

        [Test]
        public void Diverged_WithoutRecomputedRecord_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ReplayComparisonResult(
                ReplayComparisonStatus.Diverged,
                null,
                AnyDivergence(),
                null));
        }

        [Test]
        public void Diverged_WithUnavailableComponent_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ReplayComparisonResult(
                ReplayComparisonStatus.Diverged,
                "IDENTITY:CoreIdentity",
                AnyDivergence(),
                MinimalRecord()));
        }

        private static ReplayDivergence AnyDivergence()
        {
            return new ReplayDivergence(ReplayDivergenceArea.Events, 4, "EVENT[4]: stored A, recomputed B");
        }

        private static ExecutionRecord MinimalRecord()
        {
            return new ExecutionRecord(
                MinimalHeader(),
                new List<RuntimeUnitRecord>(),
                new List<EventRecord>(),
                new List<RuntimeUnitID>(),
                new List<TraceEventID>(),
                new List<TraceEventID>(),
                new SafetyCounts(0, 0, 0, 0, 0),
                SafetyStatus.Normal,
                new FinalExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0), new ScoreValue(0)),
                new List<EvidenceDefect>(),
                ExecutionCompletionStatus.Completed);
        }

        private static ExecutionEvidenceHeader MinimalHeader()
        {
            return new ExecutionEvidenceHeader(
                "EXEC:1",
                "COMP:1",
                "REV:1",
                "PROC:1",
                "CORE:1",
                "RULECFG:1",
                "SEED:1",
                new List<RevisionStamp> { new RevisionStamp("engine", "1") },
                new List<InstanceID>(),
                new List<InstanceID>(),
                new List<InstanceID>(),
                new InitialExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0)));
        }
    }
}
