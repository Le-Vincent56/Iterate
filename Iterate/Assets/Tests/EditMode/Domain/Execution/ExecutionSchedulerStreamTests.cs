using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the full per-unit SOURCE/OPERATION event stream with no Dependencies installed: each
    /// Core/Instruction unit emits exactly the eight-event stream in order — activation first,
    /// disposition finalization before completion, all in-unit, at depth zero, uncaused, mirroring
    /// the unit's ownership, host or Core-line identity, and position — while the child-iii reset,
    /// bookend, and threshold behavior holds unchanged over the new stream.
    /// </summary>
    public sealed class ExecutionSchedulerStreamTests
    {
        [Test]
        public void CoreUnit_EmitsTheEightEventStream_InOrder()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            RuntimeUnitRecord unit = record.Units[0];
            List<EventEvidence> events = UnitEvents(record, unit);

            Assert.AreEqual(8, events.Count);
            Assert.AreEqual(ExecutionEventSubtypes.SourceObjectActivated, events[0].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.SourceExecutionStarted, events[1].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.PrimaryOperationPending, events[2].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.PrimaryOperationResolved, events[3].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.QuantityAssigned, events[4].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.PrimaryOperationResultFinalized, events[5].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.SourceExecutionDispositionFinalized, events[6].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.SourceExecutionCompleted, events[7].Subtype);

            Assert.AreEqual(EventFamilies.Source, events[0].Family);
            Assert.AreEqual(EventFamilies.Source, events[1].Family);
            Assert.AreEqual(EventFamilies.Operation, events[2].Family);
            Assert.AreEqual(EventFamilies.Operation, events[3].Family);
            Assert.AreEqual(EventFamilies.Quantity, events[4].Family);
            Assert.AreEqual(EventFamilies.Operation, events[5].Family);
            Assert.AreEqual(EventFamilies.Source, events[6].Family);
            Assert.AreEqual(EventFamilies.Source, events[7].Family);

            for (int i = 0; i < events.Count; i++)
            {
                Assert.AreEqual(unit.Identity, events[i].ContainingUnit);
                Assert.AreEqual(0, events[i].CausalDepth);
                Assert.IsNull(events[i].CausingEvent);
                Assert.AreEqual(OwnershipClassification.CoreOwned, events[i].Ownership);
                Assert.AreEqual("core-01", events[i].CoreLineIdentity);
                Assert.IsNull(events[i].HostInstance);
                Assert.AreEqual(1, events[i].Position.Value.LineNumber);
            }
        }

        [Test]
        public void InstructionUnit_EmitsThePlayerOwnedStream()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            RuntimeUnitRecord unit = record.Units[1];
            List<EventEvidence> events = UnitEvents(record, unit);

            Assert.AreEqual(8, events.Count);
            Assert.AreEqual(ExecutionEventSubtypes.SourceObjectActivated, events[0].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.SourceExecutionStarted, events[1].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.QuantityChanged, events[4].Subtype);

            for (int i = 0; i < events.Count; i++)
            {
                Assert.AreEqual(unit.Identity, events[i].ContainingUnit);
                Assert.AreEqual(0, events[i].CausalDepth);
                Assert.IsNull(events[i].CausingEvent);
                Assert.AreEqual(OwnershipClassification.PlayerOwned, events[i].Ownership);
                Assert.IsNotNull(events[i].HostInstance);
                Assert.IsNull(events[i].CoreLineIdentity);
                Assert.AreEqual(2, events[i].Position.Value.LineNumber);
            }
        }

        [Test]
        public void EveryUnit_FinalizesResolvedDisposition_BeforeCompletion()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            for (int u = 0; u < record.Units.Count; u++)
            {
                List<EventEvidence> events = UnitEvents(record, record.Units[u]);

                int activated = IndexOf(events, ExecutionEventSubtypes.SourceObjectActivated);
                int started = IndexOf(events, ExecutionEventSubtypes.SourceExecutionStarted);
                int finalized = IndexOf(events, ExecutionEventSubtypes.SourceExecutionDispositionFinalized);
                int completed = IndexOf(events, ExecutionEventSubtypes.SourceExecutionCompleted);

                Assert.Less(activated, started);
                Assert.Less(finalized, completed);
                Assert.AreEqual(events.Count - 1, completed);
                Assert.AreEqual(EventDisposition.Resolved, events[finalized].Disposition);
            }
        }

        [Test]
        public void EmptySlots_EmitNothing_StreamTotalsHold()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            Assert.AreEqual(5, record.Units.Count);
            Assert.AreEqual(45, record.Events.Count);
        }

        [Test]
        public void ResetAndBookends_UnchangedOverTheNewStream()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            EventEvidence started = record.Events[0].Evidence;
            Assert.AreEqual(EventFamilies.Lifecycle, started.Family);
            Assert.AreEqual(ExecutionEventSubtypes.ExecutionStarted, started.Subtype);
            Assert.AreEqual(0, started.CausalDepth);
            Assert.IsNull(started.ContainingUnit);

            for (int i = 1; i <= 3; i++)
            {
                EventEvidence reset = record.Events[i].Evidence;
                Assert.AreEqual(EventFamilies.Quantity, reset.Family);
                Assert.AreEqual(ExecutionEventSubtypes.QuantityReset, reset.Subtype);
                Assert.AreEqual(0, reset.CausalDepth);
                Assert.IsNull(reset.ContainingUnit);
            }

            EventEvidence completed = record.Events[record.Events.Count - 1].Evidence;
            Assert.AreEqual(EventFamilies.Lifecycle, completed.Family);
            Assert.AreEqual(ExecutionEventSubtypes.ExecutionCompleted, completed.Subtype);
        }

        [Test]
        public void ThresholdCrossing_CausedByTheScoreQuantityEvent_DepthOne()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.PassCrossingRequest(new InstanceIDSource()));

            Assert.AreEqual(1, record.ThresholdHistory.Count);
            EventEvidence crossing = Find(record, record.ThresholdHistory[0]);
            RuntimeUnitRecord scoreUnit = record.Units[2];
            List<EventEvidence> unitEvents = UnitEvents(record, scoreUnit);

            Assert.AreEqual(EventFamilies.Threshold, crossing.Family);
            Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedUpward, crossing.Subtype);
            Assert.AreEqual(1, crossing.CausalDepth);
            Assert.AreEqual(ExecutionEventSubtypes.QuantityChanged, unitEvents[4].Subtype);
            Assert.AreEqual(scoreUnit.ChildEvents[4], crossing.CausingEvent);
        }

        [Test]
        public void Header_CarriesTheEmptyInstalledDependencyList()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            Assert.AreEqual(0, record.Header.InstalledDependencyInstances.Count);
        }

        [Test]
        public void DoubleRun_IsStructurallyEqual()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            ExecutionRequest request = SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource());

            ExecutionRecord first = scheduler.Execute(request);
            ExecutionRecord second = scheduler.Execute(request);

            Assert.AreEqual(first, second);
        }

        /// <summary>
        /// Returns the index of the first event carrying the subtype, failing when absent.
        /// </summary>
        /// <param name="events">The unit's events in stream order.</param>
        /// <param name="subtype">The subtype token to find.</param>
        /// <returns>The index of the first match.</returns>
        private static int IndexOf(List<EventEvidence> events, string subtype)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Subtype == subtype)
                    return i;
            }

            Assert.Fail($"No event with subtype {subtype} found in the unit stream.");
            return -1;
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
                events.Add(Find(record, unit.ChildEvents[i]));
            }

            return events;
        }

        /// <summary>
        /// Finds one event's evidence by identity.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="identity">The event identity.</param>
        /// <returns>The event's evidence.</returns>
        private static EventEvidence Find(ExecutionRecord record, TraceEventID identity)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Identity == identity)
                    return record.Events[i].Evidence;
            }

            Assert.Fail($"Event {identity} not found in the record.");
            return null;
        }
    }
}
