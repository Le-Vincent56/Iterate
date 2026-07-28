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
    /// Contract tests for <see cref="EffectResolutionProjection"/>'s timelines and boundary offers, on
    /// hand-built records. The projection answers "what did this effect origin do, and why did it not
    /// fire", reading only what the record carries: the failed requirement from the disposition reason,
    /// the observed candidate from the causing event, and the origin and socketed host from the
    /// effect-origin and host instances.
    /// The silence contract is asserted here as absence: structural ineligibility and consumed
    /// allowances emit no evidence at all, so a record with no qualification events projects no
    /// timelines. The projection reports what is recorded and never guesses at what silence meant.
    /// </summary>
    public sealed class EffectResolutionProjectionTests
    {
        [Test]
        public void Timelines_GroupOnlyTheirOwnOriginsEvents()
        {
            // Arrange — two origins resolving against two candidates.
            ExecutionRecord record = ResolutionRecord();

            // Act
            IReadOnlyList<EffectResolutionTimeline> timelines = EffectResolutionProjection.TimelinesOf(record);

            // Assert
            Assert.That(timelines.Count, Is.EqualTo(2));
            Assert.That(EntryEventValues(TimelineFor(timelines, 10)), Is.EqualTo(new[] { 2, 3, 6 }));
            Assert.That(EntryEventValues(TimelineFor(timelines, 11)), Is.EqualTo(new[] { 4 }));
        }

        [Test]
        public void Timeline_EntriesAreChronological()
        {
            // Arrange
            ExecutionRecord record = ResolutionRecord();

            // Act
            EffectResolutionTimeline timeline = TimelineFor(EffectResolutionProjection.TimelinesOf(record), 10);

            // Assert — ascending event identity, which is the record's own chronology.
            int previous = 0;
            for (int index = 0; index < timeline.Entries.Count; index++)
            {
                Assert.That(timeline.Entries[index].Event.Value, Is.GreaterThan(previous));
                previous = timeline.Entries[index].Event.Value;
            }
        }

        [Test]
        public void Timeline_CarriesTheSocketedHost()
        {
            // Arrange
            ExecutionRecord record = ResolutionRecord();

            // Act
            IReadOnlyList<EffectResolutionTimeline> timelines = EffectResolutionProjection.TimelinesOf(record);

            // Assert
            Assert.That(TimelineFor(timelines, 10).Host, Is.EqualTo(new InstanceID(20)));
            Assert.That(TimelineFor(timelines, 11).Host, Is.EqualTo(new InstanceID(21)));
        }

        [Test]
        public void NearMissEntry_CarriesItsFailedRequirementAndObservedCandidate()
        {
            // Arrange
            ExecutionRecord record = ResolutionRecord();

            // Act
            EffectResolutionEntry entry = EntryFor(EffectResolutionProjection.TimelinesOf(record), 11, 4);

            // Assert — the requirement is rendered kind:value by the engine and passed through verbatim.
            Assert.That(entry.Kind, Is.EqualTo(EffectResolutionEntryKind.FailedToQualify));
            Assert.That(entry.FailedRequirement, Is.EqualTo("PARITY:ODD"));
            Assert.That(entry.CausingEvent, Is.EqualTo(new TraceEventID(1)));
            Assert.That(entry.ContainingUnit, Is.EqualTo(new RuntimeUnitID(5)));
            Assert.That(entry.CausalDepth, Is.EqualTo(1));
        }

        [Test]
        public void QualifiedAndCommittedEntries_CarryNoRequirement()
        {
            // Arrange — the engine supplies a null disposition and reason on both, so a requirement here
            // would be invented rather than read.
            ExecutionRecord record = ResolutionRecord();
            IReadOnlyList<EffectResolutionTimeline> timelines = EffectResolutionProjection.TimelinesOf(record);

            // Act
            EffectResolutionEntry qualified = EntryFor(timelines, 10, 2);
            EffectResolutionEntry committed = EntryFor(timelines, 10, 3);

            // Assert
            Assert.That(qualified.Kind, Is.EqualTo(EffectResolutionEntryKind.Qualified));
            Assert.That(qualified.FailedRequirement, Is.Null);
            Assert.That(committed.Kind, Is.EqualTo(EffectResolutionEntryKind.Committed));
            Assert.That(committed.FailedRequirement, Is.Null);
        }

        [Test]
        public void CandidateEvents_AreNotThemselvesEntries()
        {
            // Arrange — the two candidates are Quantity-family events, not qualification events.
            ExecutionRecord record = ResolutionRecord();

            // Act
            IReadOnlyList<EffectResolutionTimeline> timelines = EffectResolutionProjection.TimelinesOf(record);

            // Assert
            int entries = 0;
            for (int index = 0; index < timelines.Count; index++)
            {
                entries += timelines[index].Entries.Count;
            }

            Assert.That(entries, Is.EqualTo(4), "the four qualification events, and neither candidate");
        }

        [Test]
        public void RecordWithNoQualificationEvents_ProjectsNoTimelines()
        {
            // Arrange — the silence case. Structural ineligibility and consumed allowances emit
            // no evidence at all, so an effect that was never a candidate is indistinguishable here from
            // one that never existed. Asserted as absence rather than described in prose: the projection
            // reports what the record carries and never guesses at what the silence meant.
            ExecutionRecord record = RecordOf(new List<EventRecord> { Candidate(1) });

            // Act
            IReadOnlyList<EffectResolutionTimeline> timelines = EffectResolutionProjection.TimelinesOf(record);

            // Assert
            Assert.That(timelines, Is.Empty);
        }

        [Test]
        public void EmptyRecord_ProjectsNoTimelinesAndNoOffers()
        {
            // Arrange
            ExecutionRecord record = RecordOf(new List<EventRecord>());

            // Act & Assert
            Assert.That(EffectResolutionProjection.TimelinesOf(record), Is.Empty);
            Assert.That(EffectResolutionProjection.OffersOf(record), Is.Empty);
        }

        [Test]
        public void Offers_GroupExactlyTheEntriesTheirCandidateCaused()
        {
            // Arrange
            ExecutionRecord record = ResolutionRecord();

            // Act
            IReadOnlyList<BoundaryOfferView> offers = EffectResolutionProjection.OffersOf(record);

            // Assert — candidate 1 was observed by three effects, candidate 5 by one.
            Assert.That(offers.Count, Is.EqualTo(2));
            Assert.That(EntryEventValues(OfferFor(offers, 1)), Is.EqualTo(new[] { 2, 3, 4 }));
            Assert.That(EntryEventValues(OfferFor(offers, 5)), Is.EqualTo(new[] { 6 }));
        }

        [Test]
        public void UncausedResolutionEntry_BelongsToNoOfferButStaysOnItsTimeline()
        {
            // Arrange — the engine does append near-misses with no candidate event: the quantity batch
            // is offered with a null candidate at depth zero. Such an entry has no offer to belong to,
            // but it is still something its origin did, so the timeline keeps it.
            List<EventRecord> events = new()
            {
                Qualification(1, ExecutionEventSubtypes.EffectFailedToQualify, 10, 20, null, null, 0, "BAND:MODIFICATION")
            };
            ExecutionRecord record = RecordOf(events);

            // Act
            IReadOnlyList<EffectResolutionTimeline> timelines = EffectResolutionProjection.TimelinesOf(record);
            IReadOnlyList<BoundaryOfferView> offers = EffectResolutionProjection.OffersOf(record);

            // Assert
            Assert.That(timelines.Count, Is.EqualTo(1));
            Assert.That(timelines[0].Entries.Count, Is.EqualTo(1));
            Assert.That(timelines[0].Entries[0].CausingEvent, Is.Null);
            Assert.That(offers, Is.Empty);
        }

        [Test]
        public void TimelineWithoutAHost_CarriesNullRatherThanAnInventedOne()
        {
            // Arrange — an effect with no socketed host records none, and the projection passes that
            // through instead of substituting the origin.
            List<EventRecord> events = new()
            {
                Qualification(1, ExecutionEventSubtypes.EffectQualified, 10, null, null, null, 0, null)
            };

            // Act
            IReadOnlyList<EffectResolutionTimeline> timelines = EffectResolutionProjection.TimelinesOf(RecordOf(events));

            // Assert
            Assert.That(timelines[0].Host, Is.Null);
        }

        [Test]
        public void NullRecord_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = EffectResolutionProjection.TimelinesOf(null));
            Assert.Throws<ArgumentException>(() => _ = EffectResolutionProjection.OffersOf(null));
        }

        /// <summary>
        /// Two candidates observed by two effect origins: candidate 1 draws a qualification and a
        /// commitment from origin 10 and a near-miss from origin 11; candidate 5 draws a near-miss from
        /// origin 10.
        /// </summary>
        /// <returns>The hand-built record.</returns>
        private static ExecutionRecord ResolutionRecord()
        {
            List<EventRecord> events = new()
            {
                Candidate(1),
                Qualification(2, ExecutionEventSubtypes.EffectQualified, 10, 20, 1, 5, 1, null),
                Qualification(3, ExecutionEventSubtypes.EffectCommitted, 10, 20, 1, 5, 1, null),
                Qualification(4, ExecutionEventSubtypes.EffectFailedToQualify, 11, 21, 1, 5, 1, "PARITY:ODD"),
                Candidate(5),
                Qualification(6, ExecutionEventSubtypes.EffectFailedToQualify, 10, 20, 5, 5, 1, "STRUCTURE_CONTEXT:ADJACENT_AFTER_SUCCESSFUL_SCORE")
            };

            return RecordOf(events);
        }

        private static EffectResolutionTimeline TimelineFor(IReadOnlyList<EffectResolutionTimeline> timelines, int origin)
        {
            for (int index = 0; index < timelines.Count; index++)
            {
                if (timelines[index].Origin == new InstanceID(origin))
                    return timelines[index];
            }

            Assert.Fail("No timeline for origin " + origin);
            return null;
        }

        private static BoundaryOfferView OfferFor(IReadOnlyList<BoundaryOfferView> offers, int candidate)
        {
            for (int index = 0; index < offers.Count; index++)
            {
                if (offers[index].CandidateEvent == new TraceEventID(candidate))
                    return offers[index];
            }

            Assert.Fail("No offer for candidate " + candidate);
            return null;
        }

        private static EffectResolutionEntry EntryFor(
            IReadOnlyList<EffectResolutionTimeline> timelines,
            int origin,
            int eventValue)
        {
            EffectResolutionTimeline timeline = TimelineFor(timelines, origin);
            for (int index = 0; index < timeline.Entries.Count; index++)
            {
                if (timeline.Entries[index].Event == new TraceEventID(eventValue))
                    return timeline.Entries[index];
            }

            Assert.Fail("No entry for event " + eventValue);
            return null;
        }

        private static int[] EntryEventValues(EffectResolutionTimeline timeline)
        {
            int[] values = new int[timeline.Entries.Count];
            for (int index = 0; index < timeline.Entries.Count; index++)
            {
                values[index] = timeline.Entries[index].Event.Value;
            }

            return values;
        }

        private static int[] EntryEventValues(BoundaryOfferView offer)
        {
            int[] values = new int[offer.Entries.Count];
            for (int index = 0; index < offer.Entries.Count; index++)
            {
                values[index] = offer.Entries[index].Event.Value;
            }

            return values;
        }

        /// <summary>
        /// A non-qualification event standing in for the boundary candidate effects observe.
        /// </summary>
        /// <param name="identity">The event identity value.</param>
        /// <returns>The event record.</returns>
        private static EventRecord Candidate(int identity)
        {
            EventEvidence evidence = new(
                EventFamilies.Quantity,
                ExecutionEventSubtypes.QuantityChanged,
                Array.Empty<string>(),
                0,
                null,
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

            return new EventRecord(new TraceEventID(identity), evidence);
        }

        /// <summary>
        /// One qualification-family event shaped exactly as the engine emits it: empty qualifiers, no
        /// parent, no Core line, no ownership, no position, no target, empty lineage, no structure
        /// context, and a disposition present only for a near-miss.
        /// </summary>
        /// <param name="identity">The event identity value.</param>
        /// <param name="subtype">The qualification subtype token.</param>
        /// <param name="origin">The effect-origin instance value.</param>
        /// <param name="host">The socketed host instance value, or null.</param>
        /// <param name="causingEvent">The observed candidate event value, or null.</param>
        /// <param name="containingUnit">The containing unit value, or null.</param>
        /// <param name="causalDepth">The causal depth.</param>
        /// <param name="failedRequirement">The failed requirement for a near-miss, or null.</param>
        /// <returns>The event record.</returns>
        private static EventRecord Qualification(
            int identity,
            string subtype,
            int origin,
            int? host,
            int? causingEvent,
            int? containingUnit,
            int causalDepth,
            string failedRequirement)
        {
            EventEvidence evidence = new(
                EventFamilies.Qualification,
                subtype,
                Array.Empty<string>(),
                causalDepth,
                containingUnit == null ? null : new RuntimeUnitID(containingUnit.Value),
                null,
                causingEvent == null ? null : new TraceEventID(causingEvent.Value),
                host == null ? null : new InstanceID(host.Value),
                null,
                new InstanceID(origin),
                null,
                null,
                null,
                EffectOriginLineage.Empty,
                null,
                0,
                null,
                failedRequirement == null ? null : EventDisposition.FailedToQualify,
                failedRequirement,
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
