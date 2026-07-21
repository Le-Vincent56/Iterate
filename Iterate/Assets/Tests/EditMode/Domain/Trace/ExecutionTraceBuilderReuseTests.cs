using System.Collections.Generic;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;
using NUnit.Framework;

namespace Iterate.Domain.Trace.Tests
{
    /// <summary>
    /// Retention and reuse tests for <see cref="ExecutionTraceBuilder"/>: two executions on one builder
    /// are independent — identities restart, no evidence leaks between them, and the frozen record of the
    /// first execution is unchanged after the second is built.
    /// </summary>
    public sealed class ExecutionTraceBuilderReuseTests
    {
        [Test]
        public void SecondExecution_RestartsIdentities_AndIsIndependentlyCorrect()
        {
            // Arrange — one builder reused across two differently shaped executions.
            ExecutionTraceBuilder builder = new();

            builder.Begin(Header("EXEC:1"));
            RuntimeUnitID firstUnit = builder.OpenUnit(CanonicalOpening());
            builder.AppendEvent(Event(EventFamilies.Lifecycle, firstUnit));
            builder.CompleteUnit(firstUnit, Closure());
            builder.Finalize(ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            // Act — a second execution with a different unit and event shape.
            builder.Begin(Header("EXEC:2"));
            RuntimeUnitID secondUnitOne = builder.OpenUnit(CanonicalOpening());
            RuntimeUnitID secondUnitTwo = builder.OpenUnit(CanonicalOpening());
            TraceEventID secondEventOne = builder.AppendEvent(Event(EventFamilies.Quantity, secondUnitOne));
            builder.CompleteUnit(secondUnitOne, Closure());
            builder.CompleteUnit(secondUnitTwo, Closure());
            ExecutionRecord second = builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            // Assert — identities restart at U1/E1, and the second record is correct on its own terms.
            Assert.That(secondUnitOne, Is.EqualTo(new RuntimeUnitID(1)));
            Assert.That(secondUnitTwo, Is.EqualTo(new RuntimeUnitID(2)));
            Assert.That(secondEventOne, Is.EqualTo(new TraceEventID(1)));
            Assert.That(second.Header.ExecutionIdentity, Is.EqualTo("EXEC:2"));
            Assert.That(second.Units.Count, Is.EqualTo(2));
            Assert.That(second.Events.Count, Is.EqualTo(1));
            Assert.That(second.Events[0].Evidence.Family, Is.EqualTo(EventFamilies.Quantity));
        }

        [Test]
        public void FirstFrozenRecord_IsUnaffectedBySecondExecution()
        {
            // Arrange
            ExecutionTraceBuilder builder = new();

            builder.Begin(Header("EXEC:1"));
            RuntimeUnitID firstUnit = builder.OpenUnit(CanonicalOpening());
            builder.AppendEvent(Event(EventFamilies.Lifecycle, firstUnit));
            builder.CompleteUnit(firstUnit, Closure());
            ExecutionRecord first = builder.Finalize(
                ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            // Act — build a second, larger execution on the same builder.
            builder.Begin(Header("EXEC:2"));
            RuntimeUnitID secondUnitOne = builder.OpenUnit(CanonicalOpening());
            RuntimeUnitID secondUnitTwo = builder.OpenUnit(CanonicalOpening());
            builder.AppendEvent(Event(EventFamilies.Quantity, secondUnitOne));
            builder.AppendEvent(Event(EventFamilies.Threshold, secondUnitTwo));
            builder.CompleteUnit(secondUnitOne, Closure());
            builder.CompleteUnit(secondUnitTwo, Closure());
            builder.Finalize(ExecutionCompletionStatus.Completed, SafetyStatus.Normal, ZeroCounts(), FinalState());

            // Assert — the first record still reports its own contents; no leakage from the second.
            Assert.That(first.Header.ExecutionIdentity, Is.EqualTo("EXEC:1"));
            Assert.That(first.Units.Count, Is.EqualTo(1));
            Assert.That(first.Events.Count, Is.EqualTo(1));
            Assert.That(first.Events[0].Evidence.Family, Is.EqualTo(EventFamilies.Lifecycle));
        }

        private static ExecutionEvidenceHeader Header(string executionIdentity)
        {
            return new ExecutionEvidenceHeader(
                executionIdentity,
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

        private static EventEvidence Event(string family, RuntimeUnitID unit)
        {
            return new EventEvidence(
                family,
                "SUBTYPE",
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
                null);
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
