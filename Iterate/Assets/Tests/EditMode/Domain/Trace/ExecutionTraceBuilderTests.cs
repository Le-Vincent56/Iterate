using System;
using System.Collections.Generic;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;
using NUnit.Framework;

namespace Iterate.Domain.Trace.Tests
{
    /// <summary>
    /// Contract tests for <see cref="ExecutionTraceBuilder"/>: append-order identity minting, freeze-time
    /// linkage and history derivation, status flow, validate-before-mutate on every method, and the
    /// aliasing-free frozen record. State and lifecycle misuse throws
    /// <see cref="InvalidOperationException"/>; bad arguments and dangling references throw
    /// <see cref="ArgumentException"/>.
    /// </summary>
    public sealed class ExecutionTraceBuilderTests
    {
        [Test]
        public void Minting_FirstUnitAndEventAreOneBased_AndStrictlyIncreasing()
        {
            // Arrange
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());

            // Act — interleaved opens and appends.
            RuntimeUnitID u1 = builder.OpenUnit(CanonicalOpening());
            TraceEventID e1 = builder.AppendEvent(Event());
            TraceEventID e2 = builder.AppendEvent(Event());
            RuntimeUnitID u2 = builder.OpenUnit(CanonicalOpening());

            // Assert
            Assert.That(u1, Is.EqualTo(new RuntimeUnitID(1)));
            Assert.That(u2, Is.EqualTo(new RuntimeUnitID(2)));
            Assert.That(e1, Is.EqualTo(new TraceEventID(1)));
            Assert.That(e2, Is.EqualTo(new TraceEventID(2)));
        }

        [Test]
        public void Finalize_DerivesChildEventsInAppendOrder()
        {
            // Arrange
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            RuntimeUnitID u1 = builder.OpenUnit(CanonicalOpening());
            TraceEventID e1 = builder.AppendEvent(Event(containingUnit: u1));
            TraceEventID e2 = builder.AppendEvent(Event(containingUnit: u1));
            builder.CompleteUnit(u1, Closure());

            // Act
            ExecutionRecord record = builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            // Assert
            Assert.That(record.Units[0].ChildEvents, Is.EqualTo(new[] { e1, e2 }));
        }

        [Test]
        public void Finalize_DerivesDescendantUnitsFromParentUnit()
        {
            // Arrange
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            RuntimeUnitID u1 = builder.OpenUnit(CanonicalOpening());
            RuntimeUnitID u2 = builder.OpenUnit(AddedOpening(u1));
            builder.CompleteUnit(u2, Closure());
            builder.CompleteUnit(u1, Closure());

            // Act
            ExecutionRecord record = builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            // Assert
            Assert.That(record.Units[0].DescendantUnits, Is.EqualTo(new[] { u2 }));
        }

        [Test]
        public void Finalize_AssignsStartOrderInOpenOrder_AndCompletionOrderInCompleteOrder()
        {
            // Arrange
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            RuntimeUnitID u1 = builder.OpenUnit(CanonicalOpening());
            RuntimeUnitID u2 = builder.OpenUnit(CanonicalOpening());
            // Complete U2 before U1 — the depth-first shape.
            builder.CompleteUnit(u2, Closure());
            builder.CompleteUnit(u1, Closure());

            // Act
            ExecutionRecord record = builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            // Assert — start order follows open order, completion order follows complete order.
            Assert.That(record.Units[0].StartOrder, Is.EqualTo(1));
            Assert.That(record.Units[1].StartOrder, Is.EqualTo(2));
            Assert.That(record.Units[1].CompletionOrder, Is.EqualTo(1));
            Assert.That(record.Units[0].CompletionOrder, Is.EqualTo(2));
        }

        [Test]
        public void Finalize_TraversalOrder_ListsOnlyCanonicalUnitsInStartOrder()
        {
            // Arrange — two canonical units and one added unit.
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            RuntimeUnitID u1 = builder.OpenUnit(CanonicalOpening());
            RuntimeUnitID u2 = builder.OpenUnit(AddedOpening(u1));
            RuntimeUnitID u3 = builder.OpenUnit(CanonicalOpening());
            builder.CompleteUnit(u2, Closure());
            builder.CompleteUnit(u3, Closure());
            builder.CompleteUnit(u1, Closure());

            // Act
            ExecutionRecord record = builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            // Assert — only the canonical units, in start order.
            Assert.That(record.TraversalOrder, Is.EqualTo(new[] { u1, u3 }));
        }

        [Test]
        public void Finalize_ThresholdHistory_CollectsThresholdFamilyEvents()
        {
            // Arrange
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            builder.OpenUnit(CanonicalOpening());
            builder.AppendEvent(Event(family: EventFamilies.Lifecycle));
            TraceEventID threshold = builder.AppendEvent(Event(family: EventFamilies.Threshold));
            builder.CompleteUnit(new RuntimeUnitID(1), Closure());

            // Act
            ExecutionRecord record = builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            // Assert
            Assert.That(record.ThresholdHistory, Is.EqualTo(new[] { threshold }));
        }

        [Test]
        public void Finalize_CounterHistory_CollectsOnlyProcessCounterQuantityEvents()
        {
            // Arrange
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            builder.OpenUnit(CanonicalOpening());
            TraceEventID counter = builder.AppendEvent(Event(
                family: EventFamilies.Quantity,
                payload: QuantityPayload(QuantityCategory.ProcessCounter)));
            builder.AppendEvent(Event(
                family: EventFamilies.Quantity,
                payload: QuantityPayload(QuantityCategory.RuntimeVariable)));
            builder.CompleteUnit(new RuntimeUnitID(1), Closure());

            // Act
            ExecutionRecord record = builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            // Assert — only the Process-counter event.
            Assert.That(record.CounterHistory, Is.EqualTo(new[] { counter }));
        }

        [Test]
        public void Finalize_NoDefects_DerivesCompleteValidEligible()
        {
            // Arrange
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());

            // Act
            ExecutionRecord record = builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            // Assert
            Assert.That(record.TraceCompleteness, Is.EqualTo(TraceCompletenessStatus.Complete));
            Assert.That(record.ResultValidity, Is.EqualTo(ResultValidityStatus.Valid));
            Assert.That(record.HandoffStatus, Is.EqualTo(ResultReviewHandoffStatus.Eligible));
        }

        [Test]
        public void Finalize_WithReportedDefect_DerivesIncompleteInvalidBlocked()
        {
            // Arrange
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            builder.ReportDefect(new EvidenceDefect(null, null, "Header", "missing field"));

            // Act
            ExecutionRecord record = builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            // Assert
            Assert.That(record.TraceCompleteness, Is.EqualTo(TraceCompletenessStatus.Incomplete));
            Assert.That(record.ResultValidity, Is.EqualTo(ResultValidityStatus.Invalid));
            Assert.That(record.HandoffStatus, Is.EqualTo(ResultReviewHandoffStatus.Blocked));
        }

        [Test]
        public void MethodsBeforeBegin_Throw()
        {
            ExecutionTraceBuilder builder = new();

            Assert.Throws<InvalidOperationException>(() => builder.OpenUnit(CanonicalOpening()));
            Assert.Throws<InvalidOperationException>(() => builder.AppendEvent(Event()));
            Assert.Throws<InvalidOperationException>(() => builder.CompleteUnit(new RuntimeUnitID(1), Closure()));
            Assert.Throws<InvalidOperationException>(() => builder.ReportDefect(new EvidenceDefect(null, null, "F", "r")));
            Assert.Throws<InvalidOperationException>(() => builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState()));
        }

        [Test]
        public void BeginTwice_Throws()
        {
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());

            Assert.Throws<InvalidOperationException>(() => builder.Begin(Header()));
        }

        [Test]
        public void NullArguments_Throw()
        {
            ExecutionTraceBuilder builder = new();

            Assert.Throws<ArgumentException>(() => builder.Begin(null));
            builder.Begin(Header());
            Assert.Throws<ArgumentException>(() => builder.OpenUnit(null));
            Assert.Throws<ArgumentException>(() => builder.AppendEvent(null));
            Assert.Throws<ArgumentException>(() => builder.CompleteUnit(new RuntimeUnitID(1), null));
            Assert.Throws<ArgumentException>(() => builder.ReportDefect(null));
        }

        [Test]
        public void AppendEvent_DanglingContainingUnit_Throws()
        {
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());

            Assert.Throws<ArgumentException>(() => builder.AppendEvent(Event(containingUnit: new RuntimeUnitID(99))));
        }

        [Test]
        public void AppendEvent_CompletedContainingUnit_Throws()
        {
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            RuntimeUnitID u1 = builder.OpenUnit(CanonicalOpening());
            builder.CompleteUnit(u1, Closure());

            Assert.Throws<ArgumentException>(() => builder.AppendEvent(Event(containingUnit: u1)));
        }

        [Test]
        public void AppendEvent_DanglingParentOrCausingEvent_Throws()
        {
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());

            Assert.Throws<ArgumentException>(() => builder.AppendEvent(Event(parentEvent: new TraceEventID(99))));
            Assert.Throws<ArgumentException>(() => builder.AppendEvent(Event(causingEvent: new TraceEventID(99))));
        }

        [Test]
        public void OpenUnit_DanglingParentUnit_Throws()
        {
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());

            Assert.Throws<ArgumentException>(() => builder.OpenUnit(AddedOpening(new RuntimeUnitID(99))));
        }

        [Test]
        public void OpenUnit_ClosedParentUnit_Throws()
        {
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            RuntimeUnitID u1 = builder.OpenUnit(CanonicalOpening());
            builder.CompleteUnit(u1, Closure());

            Assert.Throws<ArgumentException>(() => builder.OpenUnit(AddedOpening(u1)));
        }

        [Test]
        public void CompleteUnit_UnknownUnit_Throws()
        {
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());

            Assert.Throws<ArgumentException>(() => builder.CompleteUnit(new RuntimeUnitID(99), Closure()));
        }

        [Test]
        public void CompleteUnit_Twice_Throws()
        {
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            RuntimeUnitID u1 = builder.OpenUnit(CanonicalOpening());
            builder.CompleteUnit(u1, Closure());

            Assert.Throws<ArgumentException>(() => builder.CompleteUnit(u1, Closure()));
        }

        [Test]
        public void CompleteUnit_DanglingPrimaryOperationEvent_Throws()
        {
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            RuntimeUnitID u1 = builder.OpenUnit(CanonicalOpening());

            RuntimeUnitClosure closure = new(
                null,
                EventDisposition.Resolved,
                new TraceEventID(99),
                new List<string>(),
                UnitClosureStatus.NormalCompletion,
                SafetyStatus.Normal);
            Assert.Throws<ArgumentException>(() => builder.CompleteUnit(u1, closure));
        }

        [Test]
        public void ReportDefect_DanglingAnchor_Throws()
        {
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());

            Assert.Throws<ArgumentException>(() => builder.ReportDefect(
                new EvidenceDefect(new TraceEventID(99), null, "F", "r")));
            Assert.Throws<ArgumentException>(() => builder.ReportDefect(
                new EvidenceDefect(null, new RuntimeUnitID(99), "F", "r")));
        }

        [Test]
        public void Finalize_WithOpenUnit_Throws()
        {
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            builder.OpenUnit(CanonicalOpening());

            Assert.Throws<InvalidOperationException>(() => builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState()));
        }

        [Test]
        public void FailedAppend_MintsNoIdentity_AndLeavesNoRecord()
        {
            // Arrange
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            builder.OpenUnit(CanonicalOpening());
            TraceEventID e1 = builder.AppendEvent(Event());

            // Act — a dangling CausingEvent throws; the failed call must mint nothing.
            Assert.Throws<ArgumentException>(() => builder.AppendEvent(Event(causingEvent: new TraceEventID(99))));
            TraceEventID next = builder.AppendEvent(Event());
            builder.CompleteUnit(new RuntimeUnitID(1), Closure());
            ExecutionRecord record = builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            // Assert — ordinals are contiguous; the failed append left no record.
            Assert.That(e1, Is.EqualTo(new TraceEventID(1)));
            Assert.That(next, Is.EqualTo(new TraceEventID(2)));
            Assert.That(record.Events.Count, Is.EqualTo(2));
        }

        [Test]
        public void FailedOpenUnit_MintsNoIdentity()
        {
            // Arrange
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            RuntimeUnitID u1 = builder.OpenUnit(CanonicalOpening());

            // Act — a dangling parent throws; the failed open must mint nothing.
            Assert.Throws<ArgumentException>(() => builder.OpenUnit(AddedOpening(new RuntimeUnitID(99))));
            RuntimeUnitID next = builder.OpenUnit(CanonicalOpening());

            // Assert — the next unit is U2, not U3.
            Assert.That(u1, Is.EqualTo(new RuntimeUnitID(1)));
            Assert.That(next, Is.EqualTo(new RuntimeUnitID(2)));
        }

        [Test]
        public void FrozenRecord_ListsAreImmutable()
        {
            // Arrange
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            RuntimeUnitID u1 = builder.OpenUnit(CanonicalOpening());
            builder.AppendEvent(Event(containingUnit: u1));
            builder.ReportDefect(new EvidenceDefect(null, null, "F", "r"));
            builder.CompleteUnit(u1, Closure());
            ExecutionRecord record = builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            // Act & Assert — the frozen collections reject mutation.
            Assert.Throws<NotSupportedException>(() => ((IList<EventRecord>)record.Events).Add(default));
            Assert.Throws<NotSupportedException>(() => ((IList<RuntimeUnitRecord>)record.Units).Add(null));
            Assert.Throws<NotSupportedException>(() => ((IList<EvidenceDefect>)record.Defects).Add(null));
        }

        [Test]
        public void AppendAfterFinalize_Throws()
        {
            // Arrange
            ExecutionTraceBuilder builder = new();
            builder.Begin(Header());
            builder.Finalize(ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            // Act & Assert — the builder demands a new Begin.
            Assert.Throws<InvalidOperationException>(() => builder.AppendEvent(Event()));
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

        private static RuntimeUnitOpening AddedOpening(RuntimeUnitID parent)
        {
            return new RuntimeUnitOpening(
                null,
                "CORE:1",
                OwnershipClassification.CoreOwned,
                null,
                ActivationKind.AddedExecution,
                "REQ:1",
                1,
                EffectOriginLineage.Empty.Append(new InstanceID(9)),
                null,
                parent);
        }

        private static EventEvidence Event(
            string family = EventFamilies.Lifecycle,
            RuntimeUnitID? containingUnit = null,
            TraceEventID? parentEvent = null,
            TraceEventID? causingEvent = null,
            EventPayload payload = null
        )
        {
            return new EventEvidence(
                family,
                "SUBTYPE",
                new List<string>(),
                0,
                containingUnit,
                parentEvent,
                causingEvent,
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

        private static QuantityChangePayload QuantityPayload(QuantityCategory category)
        {
            return new QuantityChangePayload(
                "VALUE:1",
                category,
                QuantityOperationType.Increase,
                1,
                0,
                null,
                new List<QuantityModifierEvidence>(),
                null,
                1,
                1);
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
