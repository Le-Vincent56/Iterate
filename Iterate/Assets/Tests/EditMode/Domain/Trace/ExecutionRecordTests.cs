using System;
using System.Collections.Generic;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;
using NUnit.Framework;

namespace Iterate.Domain.Trace.Tests
{
    /// <summary>
    /// Contract tests for <see cref="ExecutionRecord"/>: the three derived statuses (completeness,
    /// validity, handoff) follow from the defect ledger and completion status at construction, final
    /// quantities are preserved separately from validity, and the safety-status/completion consistency
    /// rule is enforced. Also covers the <see cref="SafetyCounts"/> non-negative rule.
    /// </summary>
    public sealed class ExecutionRecordTests
    {
        [Test]
        public void CompletedCleanTrace_DerivesCompleteValidEligible()
        {
            // Act
            ExecutionRecord record = Build();

            // Assert
            Assert.That(record.TraceCompleteness, Is.EqualTo(TraceCompletenessStatus.Complete));
            Assert.That(record.ResultValidity, Is.EqualTo(ResultValidityStatus.Valid));
            Assert.That(record.HandoffStatus, Is.EqualTo(ResultReviewHandoffStatus.Eligible));
        }

        [Test]
        public void CompletedWithDefect_DerivesIncompleteInvalidBlocked()
        {
            // Act
            ExecutionRecord record = Build(defects: new List<EvidenceDefect> { AnyDefect() });

            // Assert
            Assert.That(record.TraceCompleteness, Is.EqualTo(TraceCompletenessStatus.Incomplete));
            Assert.That(record.ResultValidity, Is.EqualTo(ResultValidityStatus.Invalid));
            Assert.That(record.HandoffStatus, Is.EqualTo(ResultReviewHandoffStatus.Blocked));
        }

        [Test]
        public void AbortedCleanTrace_DerivesCompleteInvalidBlocked_AndPreservesFinalQuantities()
        {
            // Arrange — a cleanly traced abort: a complete trace of an aborted execution.
            FinalExecutionState finalState = new(new ValueAmount(11), new SignalValue(22), new ScoreValue(33), new ScoreValue(44));

            // Act
            ExecutionRecord record = Build(
                completionStatus: ExecutionCompletionStatus.SafetyAborted,
                safetyStatus: SafetyStatus.SafetyAborted,
                finalState: finalState);

            // Assert — the trace is complete, but the result is invalid and blocked.
            Assert.That(record.TraceCompleteness, Is.EqualTo(TraceCompletenessStatus.Complete));
            Assert.That(record.ResultValidity, Is.EqualTo(ResultValidityStatus.Invalid));
            Assert.That(record.HandoffStatus, Is.EqualTo(ResultReviewHandoffStatus.Blocked));
            // Final quantities are preserved even though the result is invalid.
            Assert.That(record.FinalState, Is.EqualTo(finalState));
        }

        [Test]
        public void AbortedWithDefect_DerivesIncompleteInvalidBlocked()
        {
            // Act
            ExecutionRecord record = Build(
                completionStatus: ExecutionCompletionStatus.SafetyAborted,
                safetyStatus: SafetyStatus.SafetyAborted,
                defects: new List<EvidenceDefect> { AnyDefect() });

            // Assert
            Assert.That(record.TraceCompleteness, Is.EqualTo(TraceCompletenessStatus.Incomplete));
            Assert.That(record.ResultValidity, Is.EqualTo(ResultValidityStatus.Invalid));
            Assert.That(record.HandoffStatus, Is.EqualTo(ResultReviewHandoffStatus.Blocked));
        }

        [Test]
        public void AbortedCompletionWithNormalSafety_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(
                completionStatus: ExecutionCompletionStatus.SafetyAborted,
                safetyStatus: SafetyStatus.Normal));
        }

        [Test]
        public void CompletedCompletionWithAbortedSafety_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = Build(
                completionStatus: ExecutionCompletionStatus.Completed,
                safetyStatus: SafetyStatus.SafetyAborted));
        }

        [Test]
        public void SafetyCounts_NegativeComponent_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new SafetyCounts(-1, 0, 0, 0, 0));
        }

        [Test]
        public void Equality_IsStructuralOverLists()
        {
            // Arrange — two independently built records with identical content.
            ExecutionRecord left = Build(defects: new List<EvidenceDefect> { AnyDefect() });
            ExecutionRecord right = Build(defects: new List<EvidenceDefect> { AnyDefect() });

            // Act & Assert
            Assert.That(right, Is.EqualTo(left));
        }

        private static ExecutionRecord Build(
            ExecutionCompletionStatus completionStatus = ExecutionCompletionStatus.Completed,
            SafetyStatus safetyStatus = SafetyStatus.Normal,
            IReadOnlyList<EvidenceDefect> defects = null,
            FinalExecutionState finalState = null
        )
        {
            return new ExecutionRecord(
                MinimalHeader(),
                new List<RuntimeUnitRecord>(),
                new List<EventRecord>(),
                new List<RuntimeUnitID>(),
                new List<TraceEventID>(),
                new List<TraceEventID>(),
                new SafetyCounts(0, 0, 0, 0, 0),
                safetyStatus,
                finalState ?? MinimalFinalState(),
                defects ?? new List<EvidenceDefect>(),
                completionStatus);
        }

        private static EvidenceDefect AnyDefect()
        {
            return new EvidenceDefect(null, null, "Header", "missing field");
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

        private static FinalExecutionState MinimalFinalState()
        {
            return new FinalExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0), new ScoreValue(0));
        }
    }
}
