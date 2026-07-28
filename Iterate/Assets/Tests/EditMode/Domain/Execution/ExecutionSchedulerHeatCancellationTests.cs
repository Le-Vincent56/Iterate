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
    /// The first end-to-end producer for the pending-request cancel site child vi wired and could not
    /// reach. A BRANCH-PREDICTOR-shaped Dependency establishes a pending request against a Condition's
    /// first contained Instruction; THERMAL THROTTLE then skips that Instruction at Heat three, so the
    /// locked host closes unsuccessfully and the request cancels.
    /// Child vi could not construct this: the only skip cause at that content was a FALSE Condition,
    /// which establishes nothing, so no locked child could close unsuccessfully. A Heat skip is the
    /// first cause that lands on a host a request is already locked to.
    /// The fixture holds two identical TRUE Conditions. The first consumes the once-per-execution
    /// allowance and its child cancels; the second qualifies nothing, which is what proves the
    /// consumed allowance stays consumed across the cancellation rather than being returned.
    /// </summary>
    public sealed class ExecutionSchedulerHeatCancellationTests
    {
        /// <summary>
        /// The Process-rule instance identity the fixture configures.
        /// </summary>
        private const int RuleInstance = 80;

        /// <summary>
        /// The source position of the Condition-contained Instruction the request locks onto.
        /// </summary>
        private const int LockedChildPosition = 6;

        /// <summary>
        /// The record of the throttled locked-child fixture.
        /// </summary>
        private ExecutionRecord _record;

        /// <summary>
        /// Runs the shared fixture once for the whole suite.
        /// </summary>
        [OneTimeSetUp]
        public void RunSharedFixture()
        {
            _record = HeatRecordReader.Execute(ThrottledLockedChildRequest());
        }

        [Test]
        public void ThrottledLockedChild_IsSkippedByTheProcessRule()
        {
            EventEvidence finalized = HeatRecordReader.DispositionAt(_record, new SourcePosition(LockedChildPosition));

            Assert.AreEqual(EventDisposition.Skipped, finalized.Disposition);
            Assert.AreEqual(2, HeatRecordReader.CountSubtype(_record, ExecutionEventSubtypes.ProcessRuleIntervened));
            Assert.AreEqual(8, _record.FinalState.FinalValue.Value, "Neither contained multiplication resolved.");
        }

        [Test]
        public void SkippedLockedHost_CancelsItsPendingRequestExactlyOnce()
        {
            Assert.AreEqual(1, HeatRecordReader.CountSubtype(_record, ExecutionEventSubtypes.AddedExecutionRequested));
            Assert.AreEqual(1, HeatRecordReader.CountSubtype(_record, ExecutionEventSubtypes.AddedExecutionCancelled));
            Assert.AreEqual(0, HeatRecordReader.CountSubtype(_record, ExecutionEventSubtypes.AddedExecutionStarted));
            Assert.AreEqual(0, _record.SafetyCounts.AddedDescendants, "A cancelled request opens no descendant.");
        }

        [Test]
        public void CancellationReason_NamesTheUnsuccessfulLockedHostDisposition()
        {
            EventEvidence cancelled = HeatRecordReader.SingleSubtype(_record, ExecutionEventSubtypes.AddedExecutionCancelled);

            Assert.AreEqual("LOCKED_HOST_UNSUCCESSFUL:SKIPPED", cancelled.DispositionReason);
            Assert.AreEqual(EventDisposition.Cancelled, cancelled.Disposition);
        }

        [Test]
        public void CancellationEvidence_MatchesTheRecordedFieldTable()
        {
            EventEvidence cancelled = HeatRecordReader.SingleSubtype(_record, ExecutionEventSubtypes.AddedExecutionCancelled);
            TraceEventID requested = IdentityOfSingleSubtype(_record, ExecutionEventSubtypes.AddedExecutionRequested);

            Assert.AreEqual(EventFamilies.AddedExecution, cancelled.Family);
            Assert.IsNull(cancelled.ContainingUnit, "The cancellation is unit-less.");
            Assert.AreEqual(requested, cancelled.CausingEvent, "Caused by the request's own requested event.");
            Assert.AreEqual(1, cancelled.CausalDepth);
            Assert.AreEqual(OwnershipClassification.PlayerOwned, cancelled.Ownership);
            Assert.AreEqual(new SourcePosition(LockedChildPosition), cancelled.Position);
            Assert.AreEqual(1, cancelled.AddedExecutionDepth);
            Assert.IsNotNull(cancelled.AddedExecutionRequestIdentity);
            Assert.IsNotNull(cancelled.EffectOriginInstance, "The creator origin rides the cancellation.");
            Assert.IsNotNull(cancelled.StructureContext, "The request's inherited Condition context is retained.");
        }

        [Test]
        public void CancelledRequestAllowance_StaysConsumed()
        {
            Assert.AreEqual(
                2,
                HeatRecordReader.CountSubtype(_record, ExecutionEventSubtypes.ConditionTrue),
                "Both Conditions evaluated true.");
            Assert.AreEqual(
                1,
                HeatRecordReader.CountSubtype(_record, ExecutionEventSubtypes.AddedExecutionRequested),
                "The second true Condition qualified nothing; the allowance was not returned.");
        }

        [Test]
        public void ThrottledCancellation_LeavesTheExecutionValidAndComplete()
        {
            Assert.AreEqual(ExecutionCompletionStatus.Completed, _record.CompletionStatus);
            Assert.AreEqual(ResultValidityStatus.Valid, _record.ResultValidity);
            Assert.AreEqual(SafetyStatus.Normal, _record.SafetyStatus);
        }

        [Test]
        public void ThrottledCancellation_IsDeterministicAcrossTwoRuns()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            ExecutionRequest request = ThrottledLockedChildRequest();

            ExecutionRecord first = scheduler.Execute(request);
            ExecutionRecord second = scheduler.Execute(request);

            Assert.AreEqual(first, second);
        }

        /// <summary>
        /// Three Core multiplications carry Heat to three, then two identical TRUE Conditions each
        /// hold one contained <c>Value ×= 2</c> Instruction. BRANCH PREDICTOR establishes a pending
        /// request for the first Condition's child; the Heat pre-check then skips that child.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest ThrottledLockedChildRequest()
        {
            StructureInstance first = SchedulerFixtures.ConditionStructure(55, CoreRegister.Value, PredicateComparison.AtLeast, 3, 2);
            StructureInstance second = SchedulerFixtures.ConditionStructure(56, CoreRegister.Value, PredicateComparison.AtLeast, 3, 2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), SchedulerFixtures.ValueCoreLine("L01", CoreLineOperator.Add, 1)),
                SourceSlot.ForCore(new SourcePosition(2), SchedulerFixtures.ValueCoreLine("L02", CoreLineOperator.Multiply, 2)),
                SourceSlot.ForCore(new SourcePosition(3), SchedulerFixtures.ValueCoreLine("L03", CoreLineOperator.Multiply, 2)),
                SourceSlot.ForCore(new SourcePosition(4), SchedulerFixtures.ValueCoreLine("L04", CoreLineOperator.Multiply, 2)),
                SourceSlot.ForStructureHeader(new SourcePosition(5), first),
                SourceSlot.ForContainedInstruction(
                    new SourcePosition(LockedChildPosition),
                    first,
                    SchedulerFixtures.ValueMultiplyInstance(57, 2)),
                SourceSlot.ForStructureHeader(new SourcePosition(7), second),
                SourceSlot.ForContainedInstruction(
                    new SourcePosition(8),
                    second,
                    SchedulerFixtures.ValueMultiplyInstance(58, 2))
            });

            return SchedulerFixtures.RequestOver(
                arrangement,
                SchedulerFixtures.ZeroState(),
                SchedulerFixtures.ConfigurationWithRule(SchedulerFixtures.ThermalThrottleRule(RuleInstance), null),
                new List<DependencyInstance> { SchedulerFixtures.BranchPredictorInstance(61) },
                new List<DirectiveInstance>());
        }

        /// <summary>
        /// Returns the trace identity of the single event carrying the given subtype.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <returns>The event identity.</returns>
        private static TraceEventID IdentityOfSingleSubtype(ExecutionRecord record, string subtype)
        {
            TraceEventID found = default;
            int count = 0;
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype != subtype)
                    continue;

                found = record.Events[i].Identity;
                count++;
            }

            Assert.AreEqual(1, count, "Expected exactly one " + subtype + " event.");

            return found;
        }
    }
}
