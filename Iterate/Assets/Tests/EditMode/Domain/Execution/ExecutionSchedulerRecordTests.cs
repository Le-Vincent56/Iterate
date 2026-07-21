using System;
using NUnit.Framework;
using Iterate.Domain.Execution;
using Iterate.Domain.Content;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the frozen record's dispositions, determinism, the causal-depth binding for a Score-band
    /// crossing, and the validate-fully-before-Begin invariant: a completed run is
    /// Completed/Complete/Valid/Eligible with zero defects, the same request run twice on one scheduler
    /// yields structurally equal records, a Pass crossing is a depth-1 threshold event caused by a depth-0
    /// quantity event, and a rejected request leaves the shared builder usable.
    /// </summary>
    public sealed class ExecutionSchedulerRecordTests
    {
        [Test]
        public void CompletedRun_IsCompleteValidEligible_ZeroDefects()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());

            ExecutionRecord record = scheduler.Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            Assert.AreEqual(ExecutionCompletionStatus.Completed, record.CompletionStatus);
            Assert.AreEqual(TraceCompletenessStatus.Complete, record.TraceCompleteness);
            Assert.AreEqual(ResultValidityStatus.Valid, record.ResultValidity);
            Assert.AreEqual(ResultReviewHandoffStatus.Eligible, record.HandoffStatus);
            Assert.AreEqual(0, record.Defects.Count);
            Assert.AreEqual(SafetyStatus.Normal, record.SafetyStatus);
        }

        [Test]
        public void SameRequestTwiceOnOneScheduler_YieldsStructurallyEqualRecords()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            ExecutionRequest request = SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource());

            ExecutionRecord first = scheduler.Execute(request);
            ExecutionRecord second = scheduler.Execute(request);

            Assert.AreEqual(first, second);
        }

        [Test]
        public void PassCrossing_IsDepthOneThresholdCausedByDepthZeroQuantity()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());

            ExecutionRecord record = scheduler.Execute(SchedulerFixtures.PassCrossingRequest(new InstanceIDSource()));

            Assert.AreEqual(1, record.ThresholdHistory.Count);

            EventEvidence crossing = FindEvent(record, record.ThresholdHistory[0]);
            Assert.AreEqual(EventFamilies.Threshold, crossing.Family);
            Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedUpward, crossing.Subtype);
            Assert.AreEqual(ProcessThresholds.PassName, crossing.TargetIdentity);
            Assert.AreEqual(1, crossing.CausalDepth);
            Assert.IsNotNull(crossing.CausingEvent);

            EventEvidence causing = FindEvent(record, crossing.CausingEvent.Value);
            Assert.AreEqual(EventFamilies.Quantity, causing.Family);
            Assert.AreEqual(0, causing.CausalDepth);

            Assert.AreEqual(0, record.Events[0].Evidence.CausalDepth);
            Assert.AreEqual(0, record.Events[record.Events.Count - 1].Evidence.CausalDepth);
        }

        [Test]
        public void RejectedRequest_LeavesBuilderUsableForNextExecution()
        {
            ExecutionTraceBuilder builder = new ExecutionTraceBuilder();
            ExecutionScheduler scheduler = new ExecutionScheduler(builder);

            Assert.Throws<ArgumentException>(() => _ = scheduler.Execute(null));

            ExecutionRecord record = scheduler.Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            Assert.AreEqual(ExecutionCompletionStatus.Completed, record.CompletionStatus);
            Assert.AreEqual(ResultValidityStatus.Valid, record.ResultValidity);
        }

        private static EventEvidence FindEvent(ExecutionRecord record, TraceEventID identity)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Identity == identity)
                    return record.Events[i].Evidence;
            }

            throw new InvalidOperationException("The referenced event was not found in the record.");
        }
    }
}
