using System.Collections.Generic;
using Iterate.Domain.Determinism;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;
using NUnit.Framework;

namespace Iterate.Domain.Trace.Tests
{
    /// <summary>
    /// Integration tests for the Determinism-to-Trace seam and the four-status conformance. A real
    /// decision is routed into event records with the canonical CAB random-selection subtype tokens (never
    /// enum member names), and its record survives storage, freeze, and recomputation with structural
    /// equality intact. The four canonical status combinations are produced end-to-end through the builder.
    /// </summary>
    public sealed class TraceDeterminismSeamTests
    {
        [Test]
        public void Seam_StoresCanonicalSubtypeTokens_NeverEnumNames()
        {
            // Arrange — a real three-candidate uniform single decision.
            DecisionResult result = new DeterminismService().Decide(BuildRequest(), BuildSnapshot(3));

            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            RuntimeUnitID unit = builder.OpenUnit(CanonicalOpening());
            foreach (RandomSelectionEvent selectionEvent in result.Events)
            {
                EventPayload payload = selectionEvent.Kind == RandomSelectionEventKind.SelectionCompleted
                    ? new RandomDecisionPayload(result.Record)
                    : null;
                builder.AppendEvent(RandomEvent(SubtypeFor(selectionEvent.Kind), unit, payload));
            }
            builder.CompleteUnit(unit, Closure());

            // Act
            ExecutionRecord record = builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            // Assert — the stored subtypes are exactly the canonical tokens, in order.
            List<string> storedSubtypes = new();
            foreach (EventRecord stored in record.Events)
                storedSubtypes.Add(stored.Evidence.Subtype);
            Assert.That(storedSubtypes, Is.EqualTo(new[]
            {
                "RANDOM_DECISION_CREATED",
                "RANDOM_CANDIDATE_SNAPSHOT_CAPTURED",
                "RANDOM_DRAW_RESOLVED",
                "RANDOM_SELECTION_COMPLETED"
            }));
        }

        [Test]
        public void Seam_DecisionRecordSurvivesStorageFreezeAndRecomputation()
        {
            // Arrange
            DecisionResult result = new DeterminismService().Decide(BuildRequest(), BuildSnapshot(3));

            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            RuntimeUnitID unit = builder.OpenUnit(CanonicalOpening());
            foreach (RandomSelectionEvent selectionEvent in result.Events)
            {
                EventPayload payload = selectionEvent.Kind == RandomSelectionEventKind.SelectionCompleted
                    ? new RandomDecisionPayload(result.Record)
                    : null;
                builder.AppendEvent(RandomEvent(SubtypeFor(selectionEvent.Kind), unit, payload));
            }
            builder.CompleteUnit(unit, Closure());
            ExecutionRecord record = builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            // Act — recover the frozen decision-bearing payload and recompute an equal decision.
            RandomDecisionRecord storedRecord = null;
            foreach (EventRecord stored in record.Events)
            {
                if (stored.Evidence.Payload is RandomDecisionPayload decisionPayload)
                    storedRecord = decisionPayload.Record;
            }
            DecisionResult recomputed = new DeterminismService().Decide(BuildRequest(), BuildSnapshot(3));

            // Assert — structural equality survives the store and matches a fresh recomputation.
            Assert.That(storedRecord, Is.Not.Null);
            Assert.That(storedRecord, Is.EqualTo(result.Record));
            Assert.That(storedRecord, Is.EqualTo(recomputed.Record));
            Assert.That(record.Events.Count, Is.EqualTo(result.Events.Count));
        }

        [Test]
        public void Conformance_CompletedClean_YieldsCompleteValidEligible()
        {
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());

            ExecutionRecord record = builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            AssertTuple(
                record,
                ExecutionCompletionStatus.Completed,
                TraceCompletenessStatus.Complete,
                ResultValidityStatus.Valid,
                ResultReviewHandoffStatus.Eligible);
        }

        [Test]
        public void Conformance_CompletedWithDefect_YieldsIncompleteInvalidBlocked()
        {
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            builder.ReportDefect(new EvidenceDefect(null, null, "Header", "missing field"));

            ExecutionRecord record = builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            AssertTuple(
                record,
                ExecutionCompletionStatus.Completed,
                TraceCompletenessStatus.Incomplete,
                ResultValidityStatus.Invalid,
                ResultReviewHandoffStatus.Blocked);
        }

        [Test]
        public void Conformance_AbortedClean_YieldsCompleteInvalidBlocked_AndPreservesFinalQuantities()
        {
            // Arrange — a cleanly traced abort: a SAFETY event and aborted status at unit and execution.
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            RuntimeUnitID unit = builder.OpenUnit(CanonicalOpening());
            builder.AppendEvent(SafetyEvent(unit));
            builder.CompleteUnit(unit, AbortedClosure());
            FinalExecutionState finalState = new(new ValueAmount(11), new SignalValue(22), new ScoreValue(33), new ScoreValue(44));

            // Act
            ExecutionRecord record = builder.Finalize(
                ExecutionCompletionStatus.SafetyAborted, SafetyStatus.SafetyAborted, ZeroCounts(), finalState);

            // Assert
            AssertTuple(
                record,
                ExecutionCompletionStatus.SafetyAborted,
                TraceCompletenessStatus.Complete,
                ResultValidityStatus.Invalid,
                ResultReviewHandoffStatus.Blocked);
            Assert.That(record.FinalState, Is.EqualTo(finalState));
        }

        [Test]
        public void Conformance_AbortedWithDefect_YieldsIncompleteInvalidBlocked()
        {
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            RuntimeUnitID unit = builder.OpenUnit(CanonicalOpening());
            builder.AppendEvent(SafetyEvent(unit));
            builder.ReportDefect(new EvidenceDefect(null, unit, "Closure", "malformed"));
            builder.CompleteUnit(unit, AbortedClosure());

            ExecutionRecord record = builder.Finalize(
                ExecutionCompletionStatus.SafetyAborted, SafetyStatus.SafetyAborted, ZeroCounts(), FinalState());

            AssertTuple(
                record,
                ExecutionCompletionStatus.SafetyAborted,
                TraceCompletenessStatus.Incomplete,
                ResultValidityStatus.Invalid,
                ResultReviewHandoffStatus.Blocked);
        }

        private static void AssertTuple(
            ExecutionRecord record,
            ExecutionCompletionStatus completion,
            TraceCompletenessStatus completeness,
            ResultValidityStatus validity,
            ResultReviewHandoffStatus handoff
        )
        {
            Assert.That(record.CompletionStatus, Is.EqualTo(completion));
            Assert.That(record.TraceCompleteness, Is.EqualTo(completeness));
            Assert.That(record.ResultValidity, Is.EqualTo(validity));
            Assert.That(record.HandoffStatus, Is.EqualTo(handoff));
        }

        private static string SubtypeFor(RandomSelectionEventKind kind)
        {
            switch (kind)
            {
                case RandomSelectionEventKind.DecisionCreated:
                    return "RANDOM_DECISION_CREATED";
                case RandomSelectionEventKind.CandidateSnapshotCaptured:
                    return "RANDOM_CANDIDATE_SNAPSHOT_CAPTURED";
                case RandomSelectionEventKind.DrawResolved:
                    return "RANDOM_DRAW_RESOLVED";
                case RandomSelectionEventKind.SelectionCompleted:
                    return "RANDOM_SELECTION_COMPLETED";
                case RandomSelectionEventKind.SelectionFailed:
                    return "RANDOM_SELECTION_FAILED";
                default:
                    return "RANDOM_FALLBACK_RESOLVED";
            }
        }

        private static DecisionRequest BuildRequest()
        {
            DecisionContext context = DecisionContext.Derive(new DecisionContextComponents(
                "SESSION-SEED-TRACE-1", null, null, null, null, null, null, null, "Random target selection", 1));
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
                context);
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

        private static ExecutionEvidenceHeader Header()
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

        private static RuntimeUnitOpening CanonicalOpening()
        {
            return new RuntimeUnitOpening(
                null,
                "CORE:1",
                OwnershipClassification.CoreOwned,
                null,
                ActivationKind.CanonicalTraversal,
                null,
                0,
                EffectOriginLineage.Empty,
                null,
                null);
        }

        private static EventEvidence RandomEvent(string subtype, RuntimeUnitID unit, EventPayload payload)
        {
            return new EventEvidence(
                EventFamilies.RandomSelection,
                subtype,
                new List<string>(),
                0,
                unit,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                EffectOriginLineage.Empty,
                null,
                0,
                null,
                null,
                null,
                SafetyStatus.Normal,
                payload);
        }

        private static EventEvidence SafetyEvent(RuntimeUnitID unit)
        {
            SafetyAbortPayload payload = new(
                "OCC:1",
                unit,
                new List<BreachedLimit> { new BreachedLimit("depth", 32, 33) });
            return new EventEvidence(
                EventFamilies.Safety,
                "SAFETY_ABORTED",
                new List<string>(),
                0,
                unit,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                EffectOriginLineage.Empty,
                null,
                0,
                null,
                null,
                null,
                SafetyStatus.SafetyAborted,
                payload);
        }

        private static RuntimeUnitClosure Closure()
        {
            return new RuntimeUnitClosure(
                null,
                EventDisposition.Resolved,
                null,
                new List<string>(),
                UnitClosureStatus.NormalCompletion,
                SafetyStatus.Normal);
        }

        private static RuntimeUnitClosure AbortedClosure()
        {
            return new RuntimeUnitClosure(
                null,
                EventDisposition.Cancelled,
                null,
                new List<string>(),
                UnitClosureStatus.SafetyIntervention,
                SafetyStatus.SafetyAborted);
        }

        private static FinalExecutionState FinalState()
        {
            return new FinalExecutionState(new ValueAmount(0), new SignalValue(0), new ScoreValue(0), new ScoreValue(0));
        }

        private static SafetyCounts ZeroCounts()
        {
            return new SafetyCounts(0, 0, 0, 0, 0);
        }
    }
}
