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
    /// Tests SAFE MODE's rescue resolution in the CAB-pinned order: the qualification, commitment,
    /// and rescued-disposition chain off the skip event, the same unit continuing to full resolution
    /// with complete host behavior — modification band and reactions included — the Rescued closure
    /// preserving the original skip, first-qualifying across multiple skips with silent exhaustion,
    /// the preserved FALSE Condition result, zero-delta consumption, the multi-rescue authoring-defect
    /// throw, and double-run determinism.
    /// </summary>
    public sealed class ExecutionSchedulerRescueTests
    {
        [Test]
        public void Rescue_EmitsThePinnedChain_AndTheSameUnitResolves()
        {
            ExecutionRecord record = Execute(RescueRequest());

            Assert.AreEqual(1, record.Units.Count);
            RuntimeUnitRecord unit = record.Units[0];
            List<EventEvidence> events = UnitEvents(record, unit);

            Assert.AreEqual(12, events.Count);
            Assert.AreEqual(ExecutionEventSubtypes.SourceObjectActivated, events[0].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.SourceExecutionStarted, events[1].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.PrimaryOperationPending, events[2].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.SourceExecutionSkipped, events[3].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.EffectQualified, events[4].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.EffectCommitted, events[5].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.SourceExecutionRescued, events[6].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.PrimaryOperationResolved, events[7].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.QuantityChanged, events[8].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.PrimaryOperationResultFinalized, events[9].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.SourceExecutionDispositionFinalized, events[10].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.SourceExecutionCompleted, events[11].Subtype);

            TraceEventID skipEvent = unit.ChildEvents[3];
            for (int i = 4; i <= 6; i++)
            {
                Assert.AreEqual(skipEvent, events[i].CausingEvent);
                Assert.AreEqual(2, events[i].CausalDepth);
                Assert.AreEqual(new InstanceID(70), events[i].EffectOriginInstance);
            }

            Assert.AreEqual(EventFamilies.Disposition, events[6].Family);
            Assert.AreEqual(EventDisposition.Rescued, events[6].Disposition);
            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
        }

        [Test]
        public void Rescue_ClosesRescued_PreservingTheSkipAndLinkingTheQuantity()
        {
            ExecutionRecord record = Execute(RescueRequest());

            RuntimeUnitRecord unit = record.Units[0];
            List<EventEvidence> events = UnitEvents(record, unit);
            RuntimeUnitClosure closure = unit.Closure;

            Assert.AreEqual(EventDisposition.Rescued, closure.FinalDisposition);
            Assert.AreEqual(EventDisposition.Skipped, closure.OriginalDisposition);
            Assert.AreEqual(unit.ChildEvents[8], closure.PrimaryOperationEvent);
            Assert.AreEqual(UnitClosureStatus.NormalCompletion, closure.Status);
            Assert.AreEqual(EventDisposition.Rescued, events[10].Disposition);
        }

        [Test]
        public void Rescue_ModificationBandApplies_ToTheRescuedOperation()
        {
            ExecutionRecord record = Execute(RescueWithStandardLibraryRequest());

            RuntimeUnitRecord unit = record.Units[0];
            List<EventEvidence> events = UnitEvents(record, unit);

            int rescued = IndexOf(events, ExecutionEventSubtypes.SourceExecutionRescued);
            int modified = IndexOf(events, ExecutionEventSubtypes.PrimaryOperationModified);
            int resolved = IndexOf(events, ExecutionEventSubtypes.PrimaryOperationResolved);

            Assert.Less(rescued, modified);
            Assert.Less(modified, resolved);
            Assert.AreEqual(new InstanceID(71), events[modified].EffectOriginInstance);
            Assert.AreEqual(unit.ChildEvents[2], events[modified].CausingEvent);
            Assert.AreEqual(1, events[modified].CausalDepth);

            int quantity = IndexOf(events, ExecutionEventSubtypes.QuantityChanged);
            QuantityChangePayload payload = (QuantityChangePayload)events[quantity].Payload;
            Assert.AreEqual(1, payload.AppliedModifiers.Count);
            Assert.AreEqual(3, payload.FinalDelta);
            Assert.AreEqual(new ValueAmount(3), record.FinalState.FinalValue);
        }

        [Test]
        public void Rescue_ReactionsFire_OffTheRescuedQuantity()
        {
            ExecutionRecord record = Execute(RescueWithReactionRequest());

            RuntimeUnitRecord unit = record.Units[0];
            List<EventEvidence> events = UnitEvents(record, unit);

            int reaction = IndexOf(events, ExecutionEventSubtypes.ImmediateReactionResolved);
            Assert.AreEqual(new InstanceID(72), events[reaction].EffectOriginInstance);
            Assert.AreEqual(new SignalValue(1), record.FinalState.FinalSignal);
            Assert.AreEqual(new ValueAmount(1), record.FinalState.FinalValue);
            Assert.AreEqual(1, record.SafetyCounts.EffectReactions);
        }

        [Test]
        public void FirstQualifying_SecondSkippedChild_StaysSkippedSilently()
        {
            ExecutionRecord record = Execute(TwoChildRescueRequest());

            Assert.AreEqual(2, record.Units.Count);
            Assert.AreEqual(EventDisposition.Rescued, record.Units[0].Closure.FinalDisposition);
            Assert.AreEqual(EventDisposition.Skipped, record.Units[1].Closure.FinalDisposition);
            Assert.IsNull(record.Units[1].Closure.OriginalDisposition);

            for (int i = 0; i < record.Events.Count; i++)
            {
                Assert.AreNotEqual(ExecutionEventSubtypes.EffectFailedToQualify, record.Events[i].Evidence.Subtype);
            }

            Assert.AreEqual(new ValueAmount(2), record.FinalState.FinalValue);
        }

        [Test]
        public void ConditionResult_StaysFalse_AfterRescue()
        {
            ExecutionRecord record = Execute(RescueRequest());

            int falseEvents = 0;
            int trueEvents = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                string subtype = record.Events[i].Evidence.Subtype;
                if (subtype == ExecutionEventSubtypes.ConditionFalse)
                    falseEvents++;

                if (subtype == ExecutionEventSubtypes.ConditionTrue)
                    trueEvents++;
            }

            Assert.AreEqual(1, falseEvents);
            Assert.AreEqual(0, trueEvents);
        }

        [Test]
        public void ZeroDeltaRescue_StillConsumesTheAllowance()
        {
            ExecutionRecord record = Execute(ZeroDeltaRescueRequest());

            Assert.AreEqual(2, record.Units.Count);
            Assert.AreEqual(EventDisposition.Rescued, record.Units[0].Closure.FinalDisposition);
            Assert.AreEqual(EventDisposition.Skipped, record.Units[1].Closure.FinalDisposition);
            Assert.AreEqual(new ValueAmount(0), record.FinalState.FinalValue);
        }

        [Test]
        public void TwoSafeModeInstances_ThrowTheMultiRescueConflict()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Execute(TwoSafeModesRequest()));

            StringAssert.Contains("rescue", exception.Message);
        }

        [Test]
        public void RescueRun_DoubleRun_IsStructurallyEqual()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            ExecutionRequest request = RescueRequest();

            ExecutionRecord first = scheduler.Execute(request);
            ExecutionRecord second = scheduler.Execute(request);

            Assert.AreEqual(first, second);
        }

        /// <summary>
        /// The FALSE-Condition rescue fixture: SAFE MODE #70 over one contained <c>Value += 2</c>
        /// Instruction (#61).
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest RescueRequest()
        {
            return ConditionRequest(
                new List<InstructionInstance> { SchedulerFixtures.ValueAddInstance(61, 2) },
                new List<DependencyInstance> { SchedulerFixtures.SafeModeInstance(70) });
        }

        /// <summary>
        /// The rescue fixture with STANDARD LIBRARY #71 also installed, so the modification band
        /// applies to the rescued operation.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest RescueWithStandardLibraryRequest()
        {
            return ConditionRequest(
                new List<InstructionInstance> { SchedulerFixtures.ValueAddInstance(61, 2) },
                new List<DependencyInstance>
                {
                    SchedulerFixtures.SafeModeInstance(70),
                    SchedulerFixtures.StandardLibraryInstance(71)
                });
        }

        /// <summary>
        /// The rescue fixture over a contained <c>Signal += 1</c> Instruction (#61) with PARALLEL
        /// CHANNEL #72 also installed, so a reaction fires off the rescued operation's quantity event.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest RescueWithReactionRequest()
        {
            return ConditionRequest(
                new List<InstructionInstance> { SchedulerFixtures.SignalAddInstance(61, 1) },
                new List<DependencyInstance>
                {
                    SchedulerFixtures.SafeModeInstance(70),
                    SchedulerFixtures.ParallelChannelInstance(72)
                });
        }

        /// <summary>
        /// The two-child rescue fixture: SAFE MODE #70 over two contained <c>Value += 2</c>
        /// Instructions (#61, #62) — first-qualifying rescues the first, the second stays skipped.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest TwoChildRescueRequest()
        {
            return ConditionRequest(
                new List<InstructionInstance>
                {
                    SchedulerFixtures.ValueAddInstance(61, 2),
                    SchedulerFixtures.ValueAddInstance(62, 2)
                },
                new List<DependencyInstance> { SchedulerFixtures.SafeModeInstance(70) });
        }

        /// <summary>
        /// The zero-delta rescue fixture: the first child's restored operation is <c>Value += 0</c> —
        /// the rescue still consumes, so the second child stays skipped.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest ZeroDeltaRescueRequest()
        {
            return ConditionRequest(
                new List<InstructionInstance>
                {
                    SchedulerFixtures.ValueAddInstance(61, 0),
                    SchedulerFixtures.ValueAddInstance(62, 2)
                },
                new List<DependencyInstance> { SchedulerFixtures.SafeModeInstance(70) });
        }

        /// <summary>
        /// Two SAFE-MODE-shaped instances (#70, #71) over one skipped child — both qualify against
        /// the first skip, the undeclared-conflict authoring defect.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest TwoSafeModesRequest()
        {
            return ConditionRequest(
                new List<InstructionInstance> { SchedulerFixtures.ValueAddInstance(61, 2) },
                new List<DependencyInstance>
                {
                    SchedulerFixtures.SafeModeInstance(70),
                    SchedulerFixtures.SafeModeInstance(71)
                });
        }

        /// <summary>
        /// Builds the standing <c>If Signal &gt;= 3</c> FALSE Condition (Structure #60, header at 1,
        /// Signal zero at entry) over the given contained children, with the given installed
        /// Dependencies.
        /// </summary>
        /// <param name="children">The contained Instruction instances in contained order.</param>
        /// <param name="installed">The installed Dependency instances.</param>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest ConditionRequest(
            List<InstructionInstance> children,
            List<DependencyInstance> installed)
        {
            StructureInstance structure = SchedulerFixtures.ConditionStructure(
                60, CoreRegister.Signal, PredicateComparison.AtLeast, 3, 1 + children.Count);

            List<SourceSlot> slots = new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure)
            };
            for (int i = 0; i < children.Count; i++)
            {
                slots.Add(SourceSlot.ForContainedInstruction(new SourcePosition(i + 2), structure, children[i]));
            }

            return SchedulerFixtures.RequestOver(
                new SourceArrangement(slots),
                SchedulerFixtures.ZeroState(),
                new InstanceIDSource(),
                installed);
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
