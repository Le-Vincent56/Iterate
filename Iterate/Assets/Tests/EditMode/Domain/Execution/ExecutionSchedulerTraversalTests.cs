using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Execution;
using Iterate.Domain.Content;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests canonical traversal over the strong-order fixture: empty slots produce no units, Core and
    /// player units carry the right ownership evidence, each unit closes Resolved/NormalCompletion with a
    /// primary-operation link, the quantity chain reads 0→1→3→5 for Value and 0→5→10 for Score, and the
    /// derived traversal order, safety counts, and safety status are correct.
    /// </summary>
    public sealed class ExecutionSchedulerTraversalTests
    {
        private static ExecutionRecord Execute(ExecutionRequest request)
        {
            return new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
        }

        [Test]
        public void TraversalOrder_ListsTheFiveNonEmptyUnits()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            Assert.AreEqual(5, record.Units.Count);
            Assert.AreEqual(5, record.TraversalOrder.Count);
        }

        [Test]
        public void CoreUnit_IsCoreOwnedWithCoreLineIdentity()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            RuntimeUnitOpening opening = record.Units[0].Opening;
            Assert.AreEqual(OwnershipClassification.CoreOwned, opening.Ownership);
            Assert.AreEqual("core-01", opening.CoreLineIdentity);
            Assert.IsNull(opening.HostInstance);
            Assert.AreEqual(ActivationKind.CanonicalTraversal, opening.Activation);
        }

        [Test]
        public void InstructionUnit_IsPlayerOwnedWithHostInstance()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            RuntimeUnitOpening opening = record.Units[1].Opening;
            Assert.AreEqual(OwnershipClassification.PlayerOwned, opening.Ownership);
            Assert.IsNotNull(opening.HostInstance);
            Assert.IsNull(opening.CoreLineIdentity);
        }

        [Test]
        public void EachUnit_ClosesResolvedNormalWithPrimaryOperationLink()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            for (int i = 0; i < record.Units.Count; i++)
            {
                RuntimeUnitClosure closure = record.Units[i].Closure;
                Assert.AreEqual(EventDisposition.Resolved, closure.FinalDisposition);
                Assert.AreEqual(UnitClosureStatus.NormalCompletion, closure.Status);
                Assert.IsNull(closure.OriginalDisposition);
                Assert.IsNotNull(closure.PrimaryOperationEvent);
            }
        }

        [Test]
        public void TraversalQuantityEvents_FollowTheStrongOrderChain()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            List<EventEvidence> quantities = InUnitQuantityEvents(record);

            Assert.AreEqual(5, quantities.Count);
            AssertQuantity(quantities[0], "Value", 0, 1);
            AssertQuantity(quantities[1], "Value", 1, 3);
            AssertQuantity(quantities[2], "Value", 3, 5);
            AssertQuantity(quantities[3], "Score", 0, 5);
            AssertQuantity(quantities[4], "Score", 5, 10);
        }

        private static List<EventEvidence> InUnitQuantityEvents(ExecutionRecord record)
        {
            List<EventEvidence> quantities = new();
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Family == EventFamilies.Quantity && evidence.ContainingUnit != null)
                    quantities.Add(evidence);
            }

            return quantities;
        }

        [Test]
        public void FinalState_IsValueFiveScoreTenOutputTen()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            Assert.AreEqual(new ValueAmount(5), record.FinalState.FinalValue);
            Assert.AreEqual(new ScoreValue(10), record.FinalState.FinalScore);
            Assert.AreEqual(new ScoreValue(10), record.FinalState.FinalOutput);
        }

        [Test]
        public void SafetyCounts_CountTheFiveUnits()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            Assert.AreEqual(new SafetyCounts(0, 0, 5, 0, 0), record.SafetyCounts);
        }

        [Test]
        public void EveryEventAndClosureAndRecord_CarryNormalSafety()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            for (int i = 0; i < record.Events.Count; i++)
                Assert.AreEqual(SafetyStatus.Normal, record.Events[i].Evidence.SafetyStatus);

            for (int i = 0; i < record.Units.Count; i++)
                Assert.AreEqual(SafetyStatus.Normal, record.Units[i].Closure.SafetyStatus);

            Assert.AreEqual(SafetyStatus.Normal, record.SafetyStatus);
        }

        private static void AssertQuantity(
            EventEvidence evidence,
            string identity,
            int prior,
            int final)
        {
            Assert.AreEqual(EventFamilies.Quantity, evidence.Family);
            Assert.AreEqual(EventDisposition.Resolved, evidence.Disposition);
            Assert.IsNotNull(evidence.ContainingUnit);

            QuantityChangePayload payload = (QuantityChangePayload)evidence.Payload;
            Assert.AreEqual(identity, payload.QuantityIdentity);
            Assert.AreEqual(prior, payload.PriorValue);
            Assert.AreEqual(final, payload.FinalValue);
        }
    }
}
