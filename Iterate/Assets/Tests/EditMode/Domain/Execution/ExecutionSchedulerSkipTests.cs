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
    /// Tests the FALSE-Condition skip path with no rescue installed: the skipped-unit stream shape,
    /// the skip event's cause and evidence, the CAB-pinned absence of operation resolution, quantity,
    /// and threshold evidence, the Skipped closure, and the untouched transformation and reaction
    /// tallies.
    /// </summary>
    public sealed class ExecutionSchedulerSkipTests
    {
        [Test]
        public void SkippedUnit_EmitsExactlyTheSkippedStream()
        {
            ExecutionRecord record = Execute(OneChildFalseRequest());

            Assert.AreEqual(1, record.Units.Count);
            List<EventEvidence> events = UnitEvents(record, record.Units[0]);

            Assert.AreEqual(6, events.Count);
            Assert.AreEqual(ExecutionEventSubtypes.SourceObjectActivated, events[0].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.SourceExecutionStarted, events[1].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.PrimaryOperationPending, events[2].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.SourceExecutionSkipped, events[3].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.SourceExecutionDispositionFinalized, events[4].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.SourceExecutionCompleted, events[5].Subtype);
            Assert.AreEqual(EventFamilies.Disposition, events[3].Family);
        }

        [Test]
        public void SkipEvent_CarriesCauseDepthAndMirroredEvidence()
        {
            ExecutionRecord record = Execute(OneChildFalseRequest());

            RuntimeUnitRecord unit = record.Units[0];
            List<EventEvidence> events = UnitEvents(record, unit);
            EventEvidence skip = events[3];

            Assert.AreEqual(unit.ChildEvents[2], skip.CausingEvent);
            Assert.AreEqual(1, skip.CausalDepth);
            Assert.AreEqual(EventDisposition.Skipped, skip.Disposition);
            Assert.AreEqual("CONDITION_FALSE:#60@1#1/eval-1", skip.DispositionReason);
            Assert.AreEqual(OwnershipClassification.PlayerOwned, skip.Ownership);
            Assert.AreEqual(new InstanceID(61), skip.HostInstance);
            Assert.AreEqual(2, skip.Position.Value.LineNumber);
            Assert.IsNotNull(skip.StructureContext);
            Assert.AreEqual("#60@1#1/eval-1", skip.StructureContext.ConditionEvaluationIdentity);
        }

        [Test]
        public void SkippedUnit_HasNoResolutionQuantityOrThresholdEvidence()
        {
            ExecutionRecord record = Execute(OneChildFalseRequest());

            List<EventEvidence> events = UnitEvents(record, record.Units[0]);
            for (int i = 0; i < events.Count; i++)
            {
                Assert.AreNotEqual(ExecutionEventSubtypes.PrimaryOperationResolved, events[i].Subtype);
                Assert.AreNotEqual(ExecutionEventSubtypes.PrimaryOperationResultFinalized, events[i].Subtype);
                Assert.AreNotEqual(ExecutionEventSubtypes.QuantityChanged, events[i].Subtype);
            }

            Assert.AreEqual(0, record.ThresholdHistory.Count);
            Assert.AreEqual(new ValueAmount(0), record.FinalState.FinalValue);
            Assert.AreEqual(EventDisposition.Skipped, events[4].Disposition);
        }

        [Test]
        public void SkippedUnit_ClosesSkipped_WithNoOriginalDispositionOrOperationLink()
        {
            ExecutionRecord record = Execute(OneChildFalseRequest());

            RuntimeUnitClosure closure = record.Units[0].Closure;

            Assert.AreEqual(EventDisposition.Skipped, closure.FinalDisposition);
            Assert.IsNull(closure.OriginalDisposition);
            Assert.IsNull(closure.PrimaryOperationEvent);
            Assert.AreEqual(UnitClosureStatus.NormalCompletion, closure.Status);
        }

        [Test]
        public void TwoChildFalseCondition_SkipsBoth_WithNoNearMissNoise()
        {
            ExecutionRecord record = Execute(TwoChildFalseRequest());

            Assert.AreEqual(2, record.Units.Count);
            Assert.AreEqual(EventDisposition.Skipped, record.Units[0].Closure.FinalDisposition);
            Assert.AreEqual(EventDisposition.Skipped, record.Units[1].Closure.FinalDisposition);

            for (int i = 0; i < record.Events.Count; i++)
            {
                Assert.AreNotEqual(ExecutionEventSubtypes.EffectFailedToQualify, record.Events[i].Evidence.Subtype);
            }

            Assert.AreEqual(new SafetyCounts(0, 0, 2, 0, 0), record.SafetyCounts);
        }

        [Test]
        public void ConditionFalse_IsEmittedBeforeAnyChildUnitOpens()
        {
            ExecutionRecord record = Execute(OneChildFalseRequest());

            int falseIndex = -1;
            int firstActivated = -1;
            for (int i = 0; i < record.Events.Count; i++)
            {
                string subtype = record.Events[i].Evidence.Subtype;
                if (subtype == ExecutionEventSubtypes.ConditionFalse && falseIndex < 0)
                    falseIndex = i;

                if (subtype == ExecutionEventSubtypes.SourceObjectActivated && firstActivated < 0)
                    firstActivated = i;
            }

            Assert.GreaterOrEqual(falseIndex, 0);
            Assert.GreaterOrEqual(firstActivated, 0);
            Assert.Less(falseIndex, firstActivated);
        }

        /// <summary>
        /// An <c>If Signal &gt;= 3</c> Condition over one contained <c>Value += 2</c> Instruction with
        /// Signal zero at entry — the child is skipped. Structure #60 header at 1, contained
        /// Instruction #61 at 2. No Dependencies installed.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest OneChildFalseRequest()
        {
            StructureInstance structure = SchedulerFixtures.ConditionStructure(60, CoreRegister.Signal, PredicateComparison.AtLeast, 3, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), structure, SchedulerFixtures.ValueAddInstance(61, 2))
            });

            return SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), new InstanceIDSource());
        }

        /// <summary>
        /// An <c>If Signal &gt;= 3</c> Condition over two contained <c>Value += 2</c> Instructions with
        /// Signal zero at entry — both children are skipped. Structure #60 header at 1, contained
        /// Instructions #61 and #62 at 2 and 3. No Dependencies installed.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest TwoChildFalseRequest()
        {
            StructureInstance structure = SchedulerFixtures.ConditionStructure(60, CoreRegister.Signal, PredicateComparison.AtLeast, 3, 3);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), structure, SchedulerFixtures.ValueAddInstance(61, 2)),
                SourceSlot.ForContainedInstruction(new SourcePosition(3), structure, SchedulerFixtures.ValueAddInstance(62, 2))
            });

            return SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), new InstanceIDSource());
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
