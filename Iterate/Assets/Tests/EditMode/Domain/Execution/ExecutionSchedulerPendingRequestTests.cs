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
    /// Tests BRANCH PREDICTOR's pending-request path through the scheduler: the request chain
    /// establishes at the Condition's success before any governed child opens, the descendant
    /// resolves after that child's own closure carrying the child as its parent and the retained
    /// evaluation identity, a successful Condition with no occupied child commits nothing, and the
    /// allowance is consumed at establishment so only the first success fires.
    /// </summary>
    public sealed class ExecutionSchedulerPendingRequestTests
    {
        [Test]
        public void ConditionSuccess_EstablishesTheRequestChainBeforeTheChildOpens()
        {
            ExecutionRecord record = Execute(BranchPredictorRequest());

            int conditionTrue = FirstGlobalIndex(record, ExecutionEventSubtypes.ConditionTrue);
            int qualified = FirstGlobalIndex(record, ExecutionEventSubtypes.EffectQualified);
            int committed = FirstGlobalIndex(record, ExecutionEventSubtypes.EffectCommitted);
            int requested = FirstGlobalIndex(record, ExecutionEventSubtypes.AddedExecutionRequested);
            int childOpens = record.Units[0].ChildEvents[0].Value - 1;

            Assert.Less(conditionTrue, qualified);
            Assert.Less(qualified, committed);
            Assert.Less(committed, requested);
            Assert.Less(requested, childOpens, "the request establishes before the governed child opens");
        }

        [Test]
        public void ConditionSuccess_RequestChain_IsUnitLess()
        {
            ExecutionRecord record = Execute(BranchPredictorRequest());

            EventEvidence requested = FindGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested);
            EventEvidence qualified = FindGlobal(record, ExecutionEventSubtypes.EffectQualified);

            Assert.IsNull(requested.ContainingUnit);
            Assert.IsNull(qualified.ContainingUnit);
            Assert.AreEqual(new InstanceID(800), requested.EffectOriginInstance);
        }

        [Test]
        public void PendingRequest_ResolvesAfterTheLockedChildCloses()
        {
            ExecutionRecord record = Execute(BranchPredictorRequest());

            Assert.AreEqual(2, record.Units.Count);
            RuntimeUnitRecord child = record.Units[0];
            RuntimeUnitRecord descendant = record.Units[1];

            Assert.AreEqual(ActivationKind.AddedExecution, descendant.Opening.Activation);
            Assert.AreEqual(child.Identity, descendant.Opening.ParentUnit);
            Assert.Greater(descendant.ChildEvents[0].Value, child.ChildEvents[child.ChildEvents.Count - 1].Value);
        }

        [Test]
        public void Descendant_CarriesTheBranchLineageAndRetainedEvaluationIdentity()
        {
            ExecutionRecord record = Execute(BranchPredictorRequest());

            RuntimeUnitOpening childOpening = record.Units[0].Opening;
            RuntimeUnitOpening descendantOpening = record.Units[1].Opening;

            Assert.AreEqual(EffectOriginLineage.Empty.Append(new InstanceID(800)), descendantOpening.Lineage);
            Assert.AreEqual(1, descendantOpening.AddedExecutionDepth);
            Assert.IsNotNull(descendantOpening.StructureContext);
            Assert.AreEqual(
                childOpening.StructureContext.ConditionEvaluationIdentity,
                descendantOpening.StructureContext.ConditionEvaluationIdentity);
        }

        [Test]
        public void PendingRequest_Registers_LandTheDuplicatedChild()
        {
            ExecutionRecord record = Execute(BranchPredictorRequest());

            Assert.AreEqual(new ValueAmount(4), record.FinalState.FinalValue);
        }

        [Test]
        public void SafetyCounts_ReportOneDescendantAtDepthOne()
        {
            ExecutionRecord record = Execute(BranchPredictorRequest());

            Assert.AreEqual(1, record.SafetyCounts.LineageDepthHighWater);
            Assert.AreEqual(1, record.SafetyCounts.AddedDescendants);
            Assert.AreEqual(2, record.SafetyCounts.SourceExecutionUnits);
        }

        [Test]
        public void SecondSuccessfulCondition_EstablishesNothing()
        {
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance first = SchedulerFixtures.ConditionStructure(60, CoreRegister.Value, PredicateComparison.IsEven, 0, 2);
            StructureInstance second = SchedulerFixtures.ConditionStructure(61, CoreRegister.Value, PredicateComparison.IsEven, 0, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), first),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), first, SchedulerFixtures.ValueAddInstance(10, 2)),
                SourceSlot.ForStructureHeader(new SourcePosition(3), second),
                SourceSlot.ForContainedInstruction(new SourcePosition(4), second, SchedulerFixtures.ValueAddInstance(11, 2))
            });

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, Installed()));

            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
        }

        [Test]
        public void EmptySuccessfulCondition_CommitsNothingAndALaterOneFires()
        {
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance empty = SchedulerFixtures.ConditionStructure(60, CoreRegister.Value, PredicateComparison.IsEven, 0, 2);
            StructureInstance occupied = SchedulerFixtures.ConditionStructure(61, CoreRegister.Value, PredicateComparison.IsEven, 0, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), empty),
                SourceSlot.ForContainedEmpty(new SourcePosition(2), empty),
                SourceSlot.ForStructureHeader(new SourcePosition(3), occupied),
                SourceSlot.ForContainedInstruction(new SourcePosition(4), occupied, SchedulerFixtures.ValueAddInstance(11, 2))
            });

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, Installed()));

            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(2, record.Units.Count);
            Assert.AreEqual(new ValueAmount(4), record.FinalState.FinalValue);
        }

        [Test]
        public void FalseCondition_EstablishesNothing()
        {
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance condition = SchedulerFixtures.ConditionStructure(60, CoreRegister.Value, PredicateComparison.AtLeast, 3, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), condition, SchedulerFixtures.ValueAddInstance(10, 2))
            });

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, Installed()));

            Assert.AreEqual(ExecutionEventSubtypes.ConditionFalse, FindGlobal(record, ExecutionEventSubtypes.ConditionFalse).Subtype);
            Assert.AreEqual(0, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(0, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionCancelled));
        }

        [Test]
        public void FalseCondition_SkippedChild_LeavesNothingPendingAtTraversalEnd()
        {
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance occupied = SchedulerFixtures.ConditionStructure(60, CoreRegister.Value, PredicateComparison.IsEven, 0, 2);
            StructureInstance blocked = SchedulerFixtures.ConditionStructure(61, CoreRegister.Value, PredicateComparison.AtLeast, 99, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), occupied),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), occupied, SchedulerFixtures.ValueAddInstance(10, 2)),
                SourceSlot.ForStructureHeader(new SourcePosition(3), blocked),
                SourceSlot.ForContainedInstruction(new SourcePosition(4), blocked, SchedulerFixtures.ValueAddInstance(11, 2))
            });

            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, Installed()));

            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(0, CountGlobal(record, ExecutionEventSubtypes.AddedExecutionCancelled));
            Assert.AreEqual(1, CountGlobal(record, ExecutionEventSubtypes.SourceExecutionSkipped));
        }

        [Test]
        public void PendingRequest_DoubleRun_IsStructurallyEqual()
        {
            ExecutionRecord first = Execute(BranchPredictorRequest());
            ExecutionRecord second = Execute(BranchPredictorRequest());

            Assert.AreEqual(first.Events.Count, second.Events.Count);
            Assert.AreEqual(first.FinalState, second.FinalState);
            for (int i = 0; i < first.Events.Count; i++)
            {
                Assert.AreEqual(first.Events[i].Evidence, second.Events[i].Evidence, "event " + i);
            }
        }

        /// <summary>
        /// An <c>If Value is even</c> Condition — TRUE at reset — over one contained
        /// <c>Value += 2</c> child, with BRANCH PREDICTOR installed.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest BranchPredictorRequest()
        {
            InstanceIDSource ids = new InstanceIDSource();
            StructureInstance condition = SchedulerFixtures.ConditionStructure(60, CoreRegister.Value, PredicateComparison.IsEven, 0, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), condition),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), condition, SchedulerFixtures.ValueAddInstance(10, 2))
            });

            return SchedulerFixtures.RequestOver(
                arrangement, SchedulerFixtures.ZeroState(), ids, Installed());
        }

        /// <summary>
        /// The standing installed set: BRANCH PREDICTOR alone.
        /// </summary>
        /// <returns>The installed Dependency instances.</returns>
        private static List<DependencyInstance> Installed()
        {
            return new List<DependencyInstance> { SchedulerFixtures.BranchPredictorInstance(800) };
        }

        /// <summary>
        /// Runs one request through a fresh scheduler.
        /// </summary>
        /// <param name="request">The request to execute.</param>
        /// <returns>The frozen record.</returns>
        private static ExecutionRecord Execute(ExecutionRequest request)
        {
            return new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
        }

        /// <summary>
        /// Finds the first record-wide event carrying the subtype.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <returns>The event's evidence.</returns>
        private static EventEvidence FindGlobal(ExecutionRecord record, string subtype)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype == subtype)
                    return record.Events[i].Evidence;
            }

            Assert.Fail($"No event carries subtype {subtype}.");
            return null;
        }

        /// <summary>
        /// Returns the record-wide index of the first event carrying the subtype.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <returns>The index.</returns>
        private static int FirstGlobalIndex(ExecutionRecord record, string subtype)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype == subtype)
                    return i;
            }

            Assert.Fail($"No event carries subtype {subtype}.");
            return -1;
        }

        /// <summary>
        /// Counts the record-wide events carrying the subtype.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <returns>The count.</returns>
        private static int CountGlobal(ExecutionRecord record, string subtype)
        {
            int count = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype == subtype)
                    count++;
            }

            return count;
        }
    }
}
