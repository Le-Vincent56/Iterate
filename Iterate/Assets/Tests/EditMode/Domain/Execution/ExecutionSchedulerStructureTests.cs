using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the phase-4 Structure walk: the canon-minimum unit-less STRUCTURE lifecycle events with
    /// their entry/iteration/evaluation contexts, Repeat iteration execution over distinct child units
    /// reading current state, Condition snapshot-at-entry evaluation with the <c>TRUE</c> path, the
    /// staged <c>FALSE</c> throw a later card retires, footprint-consuming traversal, and null
    /// contexts on top-level units.
    /// </summary>
    public sealed class ExecutionSchedulerStructureTests
    {
        [Test]
        public void RepeatTwo_EmitsTheStructureLifecycle_InOrder()
        {
            ExecutionRecord record = Execute(RepeatSignalRequest(2));

            List<EventEvidence> events = StructureEvents(record);

            Assert.AreEqual(8, events.Count);
            Assert.AreEqual(ExecutionEventSubtypes.StructureActivated, events[0].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.StructureEntered, events[1].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.RepeatCountCaptured, events[2].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.RepeatIterationStarted, events[3].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.RepeatIterationCompleted, events[4].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.RepeatIterationStarted, events[5].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.RepeatIterationCompleted, events[6].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.StructureExited, events[7].Subtype);
        }

        [Test]
        public void RepeatTwo_StructureEvents_AreUnitlessContextAttached()
        {
            ExecutionRecord record = Execute(RepeatSignalRequest(2));

            List<EventEvidence> events = StructureEvents(record);

            for (int i = 0; i < events.Count; i++)
            {
                Assert.AreEqual(EventFamilies.Structure, events[i].Family);
                Assert.IsNull(events[i].ContainingUnit);
                Assert.IsNull(events[i].CausingEvent);
                Assert.AreEqual(0, events[i].CausalDepth);
                Assert.AreEqual(OwnershipClassification.PlayerOwned, events[i].Ownership);
                Assert.AreEqual(new InstanceID(50), events[i].HostInstance);
                Assert.AreEqual(1, events[i].Position.Value.LineNumber);
                Assert.IsNull(events[i].Payload);
                Assert.IsNotNull(events[i].StructureContext);
                Assert.AreEqual("#50@1#1", events[i].StructureContext.StructureEntryIdentity);
                Assert.AreEqual(new InstanceID(50), events[i].StructureContext.StructureAncestry[0]);
            }

            Assert.IsNull(events[0].StructureContext.RepeatIterationIdentity);
            Assert.IsNull(events[1].StructureContext.RepeatIterationIdentity);
            Assert.IsNull(events[2].StructureContext.RepeatIterationIdentity);
            Assert.AreEqual("#50@1#1/iter-1", events[3].StructureContext.RepeatIterationIdentity);
            Assert.AreEqual("#50@1#1/iter-1", events[4].StructureContext.RepeatIterationIdentity);
            Assert.AreEqual("#50@1#1/iter-2", events[5].StructureContext.RepeatIterationIdentity);
            Assert.AreEqual("#50@1#1/iter-2", events[6].StructureContext.RepeatIterationIdentity);
            Assert.IsNull(events[7].StructureContext.RepeatIterationIdentity);
        }

        [Test]
        public void RepeatTwo_Children_AreDistinctUnitsSharingTheHost()
        {
            ExecutionRecord record = Execute(RepeatSignalRequest(2));

            Assert.AreEqual(2, record.Units.Count);
            RuntimeUnitOpening first = record.Units[0].Opening;
            RuntimeUnitOpening second = record.Units[1].Opening;

            Assert.AreNotEqual(record.Units[0].Identity, record.Units[1].Identity);
            Assert.AreEqual(new InstanceID(51), first.HostInstance);
            Assert.AreEqual(new InstanceID(51), second.HostInstance);
            Assert.AreEqual("#50@1#1", first.StructureContext.StructureEntryIdentity);
            Assert.AreEqual("#50@1#1", second.StructureContext.StructureEntryIdentity);
            Assert.AreEqual("#50@1#1/iter-1", first.StructureContext.RepeatIterationIdentity);
            Assert.AreEqual("#50@1#1/iter-2", second.StructureContext.RepeatIterationIdentity);
            Assert.IsNull(first.StructureContext.ConditionEvaluationIdentity);
        }

        [Test]
        public void RepeatTwo_IterationTwo_ReadsIterationOnesWrite()
        {
            ExecutionRecord record = Execute(RepeatSignalRequest(2));

            List<QuantityChangePayload> payloads = InUnitQuantityPayloads(record);

            Assert.AreEqual(2, payloads.Count);
            Assert.AreEqual(0, payloads[0].PriorValue);
            Assert.AreEqual(1, payloads[0].FinalValue);
            Assert.AreEqual(1, payloads[1].PriorValue);
            Assert.AreEqual(2, payloads[1].FinalValue);
        }

        [Test]
        public void RepeatThree_RunsThreeIterations_CompoundingSignal()
        {
            ExecutionRecord record = Execute(RepeatSignalRequest(3));

            List<EventEvidence> events = StructureEvents(record);
            List<QuantityChangePayload> payloads = InUnitQuantityPayloads(record);

            Assert.AreEqual(10, events.Count);
            Assert.AreEqual(3, record.Units.Count);
            Assert.AreEqual(3, payloads.Count);
            Assert.AreEqual(2, payloads[2].PriorValue);
            Assert.AreEqual(3, payloads[2].FinalValue);
            Assert.AreEqual(new SignalValue(3), record.FinalState.FinalSignal);
            Assert.AreEqual("#50@1#1/iter-3", record.Units[2].Opening.StructureContext.RepeatIterationIdentity);
        }

        [Test]
        public void EmptyRepeat_EmitsTheFullLifecycle_AndNoChildUnits()
        {
            ExecutionRecord record = Execute(EmptyRepeatRequest());

            List<EventEvidence> events = StructureEvents(record);

            Assert.AreEqual(8, events.Count);
            Assert.AreEqual(ExecutionEventSubtypes.StructureActivated, events[0].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.RepeatCountCaptured, events[2].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.RepeatIterationStarted, events[3].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.RepeatIterationCompleted, events[4].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.RepeatIterationStarted, events[5].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.RepeatIterationCompleted, events[6].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.StructureExited, events[7].Subtype);
            Assert.AreEqual(0, record.Units.Count);
        }

        [Test]
        public void ConditionTrue_EmitsSnapshotThenTrue_ChildCarriesTheEvaluationIdentity()
        {
            ExecutionRecord record = Execute(TrueConditionRequest());

            List<EventEvidence> events = StructureEvents(record);

            Assert.AreEqual(5, events.Count);
            Assert.AreEqual(ExecutionEventSubtypes.StructureActivated, events[0].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.StructureEntered, events[1].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.ConditionSnapshotCaptured, events[2].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.ConditionTrue, events[3].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.StructureExited, events[4].Subtype);

            Assert.AreEqual("#60@1#1/eval-1", events[2].StructureContext.ConditionEvaluationIdentity);
            Assert.AreEqual("#60@1#1/eval-1", events[3].StructureContext.ConditionEvaluationIdentity);

            Assert.AreEqual(1, record.Units.Count);
            RuntimeUnitOpening opening = record.Units[0].Opening;
            Assert.AreEqual("#60@1#1", opening.StructureContext.StructureEntryIdentity);
            Assert.AreEqual("#60@1#1/eval-1", opening.StructureContext.ConditionEvaluationIdentity);
            Assert.IsNull(opening.StructureContext.RepeatIterationIdentity);
            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
        }

        [Test]
        public void ConditionSnapshot_AtEntry_ImmuneToChildRegisterChanges()
        {
            ExecutionRecord record = Execute(SnapshotImmunityRequest());

            int trueEvents = 0;
            int falseEvents = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                string subtype = record.Events[i].Evidence.Subtype;
                if (subtype == ExecutionEventSubtypes.ConditionTrue)
                    trueEvents++;

                if (subtype == ExecutionEventSubtypes.ConditionFalse)
                    falseEvents++;
            }

            Assert.AreEqual(1, trueEvents);
            Assert.AreEqual(0, falseEvents);
            Assert.AreEqual(2, record.Units.Count);
            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
        }

        [Test]
        public void EmptyCondition_EvaluatesAndExits_NoChildEvents()
        {
            ExecutionRecord record = Execute(EmptyConditionRequest());

            List<EventEvidence> events = StructureEvents(record);

            Assert.AreEqual(5, events.Count);
            Assert.AreEqual(ExecutionEventSubtypes.ConditionSnapshotCaptured, events[2].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.ConditionTrue, events[3].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.StructureExited, events[4].Subtype);
            Assert.AreEqual(0, record.Units.Count);
        }

        [Test]
        public void Traversal_ResumesPastTheFootprint_TopLevelUnitKeepsNullContext()
        {
            ExecutionRecord record = Execute(RepeatThenTopLevelRequest());

            Assert.AreEqual(3, record.Units.Count);
            RuntimeUnitRecord topLevel = record.Units[2];
            Assert.AreEqual(3, topLevel.Opening.Position.Value.LineNumber);
            Assert.IsNull(topLevel.Opening.StructureContext);

            List<EventEvidence> events = UnitEvents(record, topLevel);
            for (int i = 0; i < events.Count; i++)
            {
                Assert.IsNull(events[i].StructureContext);
            }

            Assert.AreEqual(new SignalValue(2), record.FinalState.FinalSignal);
            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
        }

        [Test]
        public void Repeat2WithStandardLibrary_ModifiesBothIterations_OneCommitment()
        {
            ExecutionRecord record = Execute(RepeatWithStandardLibraryRequest());

            List<QuantityChangePayload> payloads = InUnitQuantityPayloads(record);
            Assert.AreEqual(2, payloads.Count);
            Assert.AreEqual(0, payloads[0].PriorValue);
            Assert.AreEqual(3, payloads[0].FinalValue);
            Assert.AreEqual(1, payloads[0].AppliedModifiers.Count);
            Assert.AreEqual(3, payloads[1].PriorValue);
            Assert.AreEqual(6, payloads[1].FinalValue);
            Assert.AreEqual(1, payloads[1].AppliedModifiers.Count);
            Assert.AreEqual(new ValueAmount(6), record.FinalState.FinalValue);

            int committed = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype == ExecutionEventSubtypes.EffectCommitted)
                    committed++;
            }

            Assert.AreEqual(1, committed);
        }

        [Test]
        public void StructureRun_DoubleRun_IsStructurallyEqual()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            ExecutionRequest request = RepeatThenTopLevelRequest();

            ExecutionRecord first = scheduler.Execute(request);
            ExecutionRecord second = scheduler.Execute(request);

            Assert.AreEqual(first, second);
        }

        /// <summary>
        /// A Repeat over one contained <c>Signal += 1</c> Instruction: Structure #50 header at 1,
        /// contained Instruction #51 at 2.
        /// </summary>
        /// <param name="count">The authored iteration count.</param>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest RepeatSignalRequest(int count)
        {
            StructureInstance structure = SchedulerFixtures.RepeatStructure(50, count, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), structure, SchedulerFixtures.SignalAddInstance(51, 1))
            });

            return SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), new InstanceIDSource());
        }

        /// <summary>
        /// A <c>Repeat 2</c> whose single contained position is empty: Structure #50 header at 1,
        /// contained empty at 2.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest EmptyRepeatRequest()
        {
            StructureInstance structure = SchedulerFixtures.RepeatStructure(50, 2, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedEmpty(new SourcePosition(2), structure)
            });

            return SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), new InstanceIDSource());
        }

        /// <summary>
        /// An <c>If Value is even</c> Condition over one contained <c>Value += 2</c> Instruction —
        /// Value is zero at entry, so the predicate evaluates <c>TRUE</c>. Structure #60 header at 1,
        /// contained Instruction #61 at 2.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest TrueConditionRequest()
        {
            StructureInstance structure = SchedulerFixtures.ConditionStructure(60, CoreRegister.Value, PredicateComparison.IsEven, 0, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), structure, SchedulerFixtures.ValueAddInstance(61, 2))
            });

            return SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), new InstanceIDSource());
        }

        /// <summary>
        /// An <c>If Value is even</c> Condition over two contained <c>Value += 1</c> Instructions —
        /// the first child makes Value odd, so a reevaluation would flip the result; the snapshot at
        /// entry keeps both children governed <c>TRUE</c>. Structure #60 header at 1, contained
        /// Instructions #62 and #63 at 2 and 3.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest SnapshotImmunityRequest()
        {
            StructureInstance structure = SchedulerFixtures.ConditionStructure(60, CoreRegister.Value, PredicateComparison.IsEven, 0, 3);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), structure, SchedulerFixtures.ValueAddInstance(62, 1)),
                SourceSlot.ForContainedInstruction(new SourcePosition(3), structure, SchedulerFixtures.ValueAddInstance(63, 1))
            });

            return SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), new InstanceIDSource());
        }

        /// <summary>
        /// An <c>If Value is even</c> Condition whose single contained position is empty. Structure #60
        /// header at 1, contained empty at 2.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest EmptyConditionRequest()
        {
            StructureInstance structure = SchedulerFixtures.ConditionStructure(60, CoreRegister.Value, PredicateComparison.IsEven, 0, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedEmpty(new SourcePosition(2), structure)
            });

            return SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), new InstanceIDSource());
        }

        /// <summary>
        /// A <c>Repeat 2</c> footprint followed by a top-level <c>Value += 2</c> Instruction:
        /// Structure #50 header at 1, contained Instruction #51 at 2, top-level Instruction #52 at 3.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest RepeatThenTopLevelRequest()
        {
            StructureInstance structure = SchedulerFixtures.RepeatStructure(50, 2, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), structure, SchedulerFixtures.SignalAddInstance(51, 1)),
                SourceSlot.ForInstruction(new SourcePosition(3), SchedulerFixtures.ValueAddInstance(52, 2))
            });

            return SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), new InstanceIDSource());
        }

        /// <summary>
        /// A <c>Repeat 2</c> over one eligible fixed-number player <c>Value += 2</c> Instruction with
        /// a STANDARD-LIBRARY-shaped instance (#100) installed — the selected-host observable.
        /// Structure #50 header at 1, contained Instruction #51 at 2.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest RepeatWithStandardLibraryRequest()
        {
            StructureInstance structure = SchedulerFixtures.RepeatStructure(50, 2, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), structure, SchedulerFixtures.ValueAddInstance(51, 2))
            });

            return SchedulerFixtures.RequestOver(
                arrangement,
                SchedulerFixtures.ZeroState(),
                new InstanceIDSource(),
                new List<DependencyInstance> { SchedulerFixtures.StandardLibraryInstance(100) });
        }

        /// <summary>
        /// Executes a request on a fresh scheduler and builder.
        /// </summary>
        /// <param name="request">The request to execute.</param>
        /// <returns>The frozen record.</returns>
        private static ExecutionRecord Execute(ExecutionRequest request)
        {
            return new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
        }

        /// <summary>
        /// Collects the STRUCTURE-family events in record order.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <returns>The Structure events in order.</returns>
        private static List<EventEvidence> StructureEvents(ExecutionRecord record)
        {
            List<EventEvidence> events = new List<EventEvidence>();
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Family == EventFamilies.Structure)
                    events.Add(record.Events[i].Evidence);
            }

            return events;
        }

        /// <summary>
        /// Collects the in-unit QUANTITY payloads in record order.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <returns>The quantity payloads in order.</returns>
        private static List<QuantityChangePayload> InUnitQuantityPayloads(ExecutionRecord record)
        {
            List<QuantityChangePayload> payloads = new List<QuantityChangePayload>();
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Family == EventFamilies.Quantity && evidence.ContainingUnit != null)
                    payloads.Add((QuantityChangePayload)evidence.Payload);
            }

            return payloads;
        }

        /// <summary>
        /// Resolves a unit's child events to their evidence in stream order.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="unit">The unit whose events to resolve.</param>
        /// <returns>The unit's event evidence in order.</returns>
        private static List<EventEvidence> UnitEvents(ExecutionRecord record, RuntimeUnitRecord unit)
        {
            List<EventEvidence> events = new List<EventEvidence>(unit.ChildEvents.Count);
            for (int i = 0; i < unit.ChildEvents.Count; i++)
            {
                for (int j = 0; j < record.Events.Count; j++)
                {
                    if (record.Events[j].Identity == unit.ChildEvents[i])
                    {
                        events.Add(record.Events[j].Evidence);
                        break;
                    }
                }
            }

            Assert.AreEqual(unit.ChildEvents.Count, events.Count);
            return events;
        }
    }
}
