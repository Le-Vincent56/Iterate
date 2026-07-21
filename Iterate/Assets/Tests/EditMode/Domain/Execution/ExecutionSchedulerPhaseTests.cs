using NUnit.Framework;
using Iterate.Domain.Execution;
using Iterate.Domain.Content;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the scheduler's phase ordering and bookends: the <c>EXECUTION_STARTED</c> lifecycle event
    /// precedes the three neutral resets, the resets carry the initial-state priors and reset to zero
    /// outside any unit, the <c>EXECUTION_COMPLETED</c> lifecycle event is last, and the header lands with
    /// the configuration identities, revision stamps, and pragma instances.
    /// </summary>
    public sealed class ExecutionSchedulerPhaseTests
    {
        private static ExecutionRecord Execute(ExecutionRequest request)
        {
            return new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
        }

        [Test]
        public void FirstEvent_IsExecutionStartedLifecycle()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            Assert.AreEqual(EventFamilies.Lifecycle, record.Events[0].Evidence.Family);
            Assert.AreEqual(ExecutionEventSubtypes.ExecutionStarted, record.Events[0].Evidence.Subtype);
            Assert.AreEqual(0, record.Events[0].Evidence.CausalDepth);
            Assert.IsNull(record.Events[0].Evidence.ContainingUnit);
        }

        [Test]
        public void ResetEvents_AreValueSignalScoreToZero_WithInitialPriors()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.State(5, 2, 7), new InstanceIDSource()));

            AssertReset(record.Events[1].Evidence, "Value", 5);
            AssertReset(record.Events[2].Evidence, "Signal", 2);
            AssertReset(record.Events[3].Evidence, "Score", 7);
        }

        [Test]
        public void LastEvent_IsExecutionCompletedLifecycle()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource()));

            EventEvidence last = record.Events[record.Events.Count - 1].Evidence;
            Assert.AreEqual(EventFamilies.Lifecycle, last.Family);
            Assert.AreEqual(ExecutionEventSubtypes.ExecutionCompleted, last.Subtype);
            Assert.AreEqual(0, last.CausalDepth);
        }

        [Test]
        public void Header_CarriesConfigurationIdentitiesStampsAndPragmas()
        {
            InstanceIDSource ids = new InstanceIDSource();
            ExecutionRequest request = SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), ids);

            ExecutionRecord record = Execute(request);

            Assert.AreEqual("exec", record.Header.ExecutionIdentity);
            Assert.AreEqual("compilation", record.Header.CompilationIdentity);
            Assert.AreEqual("core", record.Header.CoreIdentity);
            Assert.AreEqual("session-seed", record.Header.SessionSeedIdentity);
            Assert.AreEqual(request.RevisionStamps.Count, record.Header.RevisionStamps.Count);
            Assert.AreEqual(request.Source.Pragmas.Count, record.Header.ActiveDirectiveInstances.Count);
            Assert.AreEqual(request.Source.Pragmas[0].InstanceID, record.Header.ActiveDirectiveInstances[0]);
            Assert.AreEqual(0, record.Header.InstalledDependencyInstances.Count);
            Assert.AreEqual(0, record.Header.RelevantPatchInstances.Count);
        }

        private static void AssertReset(EventEvidence evidence, string identity, int prior)
        {
            Assert.AreEqual(EventFamilies.Quantity, evidence.Family);
            Assert.AreEqual(ExecutionEventSubtypes.QuantityReset, evidence.Subtype);
            Assert.IsNull(evidence.ContainingUnit);
            Assert.AreEqual(0, evidence.CausalDepth);

            QuantityChangePayload payload = (QuantityChangePayload)evidence.Payload;
            Assert.AreEqual(identity, payload.QuantityIdentity);
            Assert.AreEqual(QuantityOperationType.Assign, payload.Operation);
            Assert.AreEqual(prior, payload.PriorValue);
            Assert.AreEqual(0, payload.FinalValue);
            Assert.AreEqual(QuantityCategory.RuntimeVariable, payload.Category);
        }
    }
}
