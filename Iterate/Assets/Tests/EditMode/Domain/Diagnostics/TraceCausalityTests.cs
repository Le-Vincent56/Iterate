using System;
using System.Collections.Generic;
using Iterate.Domain.Diagnostics;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;
using NUnit.Framework;

namespace Iterate.Domain.Diagnostics.Tests
{
    /// <summary>
    /// Contract tests for <see cref="TraceCausality"/>: the two linkages a record carries are distinct
    /// and are indexed separately. Causation is the causing-event chain — what an event happened
    /// because of — and containment is the parent-event chain, which is what an event happened inside.
    /// An event may have either, both, or neither, and an event with neither answers empty rather than
    /// throwing, because a root candidate is a normal thing for a debugger to select.
    /// </summary>
    public sealed class TraceCausalityTests
    {
        [Test]
        public void AncestorsOf_WalksTheCausingChainToTheRoot()
        {
            // Arrange — 1 causes 2, 2 causes 3, 3 causes 4.
            TraceCausality causality = new(ChainRecord());

            // Act
            IReadOnlyList<TraceEventID> ancestors = causality.AncestorsOf(new TraceEventID(4));

            // Assert — nearest cause first, root last.
            Assert.That(Values(ancestors), Is.EqualTo(new[] { 3, 2, 1 }));
        }

        [Test]
        public void AncestorsOf_ImmediatelyCausedEvent_IsJustItsCause()
        {
            TraceCausality causality = new(ChainRecord());

            Assert.That(Values(causality.AncestorsOf(new TraceEventID(2))), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void AncestorsOf_UncausedEvent_IsEmpty()
        {
            // The root of a causal chain is uncaused; that is an answer, not a missing one.
            TraceCausality causality = new(ChainRecord());

            Assert.That(causality.AncestorsOf(new TraceEventID(1)), Is.Empty);
        }

        [Test]
        public void ConsequencesOf_ListsEveryDirectlyCausedEventInOrder()
        {
            // Arrange — 1 directly causes 2 and 5; 5 is not a consequence of 2.
            TraceCausality causality = new(FanOutRecord());

            // Act
            IReadOnlyList<TraceEventID> consequences = causality.ConsequencesOf(new TraceEventID(1));

            // Assert
            Assert.That(Values(consequences), Is.EqualTo(new[] { 2, 5 }));
        }

        [Test]
        public void ConsequencesOf_AreDirectOnly_NotTransitive()
        {
            // Arrange — 2 causes 3, so 3 is a consequence of 2 but not of 1.
            TraceCausality causality = new(FanOutRecord());

            // Act & Assert — 1's consequences are exactly 2 and 5, which is the strongest way to say
            // that 3 is not among them: a transitive implementation would list it here.
            Assert.That(Values(causality.ConsequencesOf(new TraceEventID(2))), Is.EqualTo(new[] { 3 }));
            Assert.That(Values(causality.ConsequencesOf(new TraceEventID(1))), Is.EqualTo(new[] { 2, 5 }));
        }

        [Test]
        public void ConsequencesOf_LeafEvent_IsEmpty()
        {
            TraceCausality causality = new(FanOutRecord());

            Assert.That(causality.ConsequencesOf(new TraceEventID(5)), Is.Empty);
        }

        [Test]
        public void ContainmentChildrenOf_ListsEveryEventNamingItAsParent()
        {
            // Arrange — 10 contains 11 and 12; containment is recorded separately from causation, and
            // this record deliberately gives them different shapes so one cannot stand in for the other.
            TraceCausality causality = new(ContainmentRecord());

            // Act
            IReadOnlyList<TraceEventID> children = causality.ContainmentChildrenOf(new TraceEventID(10));

            // Assert
            Assert.That(Values(children), Is.EqualTo(new[] { 11, 12 }));
        }

        [Test]
        public void ContainmentChildrenOf_ChildlessEvent_IsEmpty()
        {
            TraceCausality causality = new(ContainmentRecord());

            Assert.That(causality.ContainmentChildrenOf(new TraceEventID(11)), Is.Empty);
        }

        [Test]
        public void ContainmentAndCausationAreIndexedSeparately()
        {
            // Arrange — in this record 11's parent is 10 while its cause is 13, so an implementation
            // that indexed one and answered both would fail here.
            TraceCausality causality = new(ContainmentRecord());

            // Act & Assert
            Assert.That(Values(causality.ContainmentChildrenOf(new TraceEventID(10))), Is.EqualTo(new[] { 11, 12 }));
            Assert.That(Values(causality.AncestorsOf(new TraceEventID(11))), Is.EqualTo(new[] { 13 }));
            Assert.That(causality.ConsequencesOf(new TraceEventID(10)), Is.Empty);
        }

        [Test]
        public void UnknownEvent_AnswersEmptyRatherThanThrowing()
        {
            // A viewer may hold an identity from another record; answering empty keeps the projection
            // read-only and total.
            TraceCausality causality = new(ChainRecord());

            Assert.That(causality.AncestorsOf(new TraceEventID(999)), Is.Empty);
            Assert.That(causality.ConsequencesOf(new TraceEventID(999)), Is.Empty);
            Assert.That(causality.ContainmentChildrenOf(new TraceEventID(999)), Is.Empty);
        }

        [Test]
        public void EmptyRecord_AnswersEmpty()
        {
            TraceCausality causality = new(RecordOf(new List<EventRecord>()));

            Assert.That(causality.AncestorsOf(new TraceEventID(1)), Is.Empty);
        }

        [Test]
        public void CyclicCausingChain_TerminatesRatherThanHanging()
        {
            // Arrange — 1 causes 2 and 2 causes 1. Engine-unreachable: causation runs from an earlier
            // event to a later one. But nothing validates acyclicity — not the event constructor, which
            // sees one event at a time, and not the record constructor, which never inspects the links —
            // so only a defectively-stored record reaches this. A diagnostic asked to explain a corrupt
            // record must answer, not hang, which is exactly when a debugger is most needed.
            TraceCausality causality = new(RecordOf(new List<EventRecord>
            {
                Event(1, 2, null),
                Event(2, 1, null)
            }));

            // Act
            IReadOnlyList<TraceEventID> ancestors = causality.AncestorsOf(new TraceEventID(1));

            // Assert — bounded by the event count, so the walk stops instead of looping forever.
            Assert.That(ancestors.Count, Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void NullRecord_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new TraceCausality(null));
        }

        /// <summary>
        /// A straight causal chain: 1 → 2 → 3 → 4.
        /// </summary>
        /// <returns>The hand-built record.</returns>
        private static ExecutionRecord ChainRecord()
        {
            return RecordOf(new List<EventRecord>
            {
                Event(1, null, null),
                Event(2, 1, null),
                Event(3, 2, null),
                Event(4, 3, null)
            });
        }

        /// <summary>
        /// A fan-out: 1 causes 2 and 5; 2 causes 3.
        /// </summary>
        /// <returns>The hand-built record.</returns>
        private static ExecutionRecord FanOutRecord()
        {
            return RecordOf(new List<EventRecord>
            {
                Event(1, null, null),
                Event(2, 1, null),
                Event(3, 2, null),
                Event(5, 1, null)
            });
        }

        /// <summary>
        /// Containment crossed with causation: 10 contains 11 and 12, while 11 is caused by 13.
        /// </summary>
        /// <returns>The hand-built record.</returns>
        private static ExecutionRecord ContainmentRecord()
        {
            return RecordOf(new List<EventRecord>
            {
                Event(10, null, null),
                Event(11, 13, 10),
                Event(12, null, 10),
                Event(13, null, null)
            });
        }

        private static int[] Values(IReadOnlyList<TraceEventID> identities)
        {
            int[] values = new int[identities.Count];
            for (int index = 0; index < identities.Count; index++)
            {
                values[index] = identities[index].Value;
            }

            return values;
        }

        /// <summary>
        /// One lifecycle event carrying only the two linkages under test.
        /// </summary>
        /// <param name="identity">The event identity value.</param>
        /// <param name="causingEvent">The causing event value, or null when uncaused.</param>
        /// <param name="parentEvent">The containing parent event value, or null when unparented.</param>
        /// <returns>The event record.</returns>
        private static EventRecord Event(int identity, int? causingEvent, int? parentEvent)
        {
            EventEvidence evidence = new(
                EventFamilies.Qualification,
                ExecutionEventSubtypes.EffectQualified,
                Array.Empty<string>(),
                0,
                null,
                parentEvent == null ? null : new TraceEventID(parentEvent.Value),
                causingEvent == null ? null : new TraceEventID(causingEvent.Value),
                null,
                null,
                new InstanceID(1),
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

            return new EventRecord(new TraceEventID(identity), evidence);
        }

        private static ExecutionRecord RecordOf(IReadOnlyList<EventRecord> events)
        {
            return new ExecutionRecord(
                MinimalHeader(),
                new List<RuntimeUnitRecord>(),
                events,
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
