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
    /// The counter-evidence obligations behind the Heat matrix: initialization and reset, the
    /// per-commit evidence CAB-EVT-742/745 require, the counter history the frozen record derives,
    /// the transformation counts the skip and the rescue re-anchor, status derived from the value
    /// rather than stored, and the control case where no Process rule is configured at all.
    /// The control matters more than it looks. A null Process rule leaves the counter snapshot absent
    /// on every occurrence, which makes the counter effects structurally ineligible rather than
    /// near-missing — so the absence of counter evidence in an ordinary execution is a positive
    /// result, not a silence.
    /// CAB-EVT-753's final row, maximum-speed and reduced-effects comprehension, is a presentation
    /// obligation with no engine-side surface. It is not asserted here and no test pretends it is;
    /// the event granularity it must not contradict is what this suite pins.
    /// </summary>
    public sealed class ExecutionSchedulerHeatEvidenceTests
    {
        /// <summary>
        /// The Process-rule instance identity every fixture in this suite configures.
        /// </summary>
        private const int RuleInstance = 80;

        /// <summary>
        /// The record of the throttling ladder: three gains, then one skip at the ceiling.
        /// </summary>
        private ExecutionRecord _ladder;

        /// <summary>
        /// Runs the shared ladder once for the whole suite.
        /// </summary>
        [OneTimeSetUp]
        public void RunSharedFixture()
        {
            _ladder = HeatRecordReader.Execute(LadderRequest(false));
        }

        [Test]
        public void HeatInitializes_ToZeroWithEvidenceDistinctFromLaterChanges()
        {
            EventEvidence initialization = HeatRecordReader.CounterInitialization(_ladder);
            QuantityChangePayload payload = (QuantityChangePayload)initialization.Payload;

            Assert.AreEqual(ExecutionEventSubtypes.QuantityReset, initialization.Subtype);
            Assert.AreEqual(EventFamilies.Quantity, initialization.Family);
            Assert.AreEqual(HeatRecordReader.HeatIdentity, payload.QuantityIdentity);
            Assert.AreEqual(0, payload.PriorValue);
            Assert.AreEqual(0, payload.FinalValue);
            Assert.AreEqual(0, payload.FinalDelta);
        }

        [Test]
        public void HeatInitialization_PrecedesSourceTraversal()
        {
            int initializationIndex = FirstIndexOfCounterReset(_ladder);
            int firstActivationIndex = FirstIndexOfSubtype(_ladder, ExecutionEventSubtypes.SourceObjectActivated);

            Assert.Greater(initializationIndex, -1, "The counter initializes when a Process rule is configured.");
            Assert.Less(initializationIndex, firstActivationIndex, "Heat becomes zero before traversal begins.");
        }

        [Test]
        public void NoProcessRule_EmitsNoCounterEvidenceAtAll()
        {
            ExecutionRecord record = HeatRecordReader.Execute(RuleFreeLadderRequest());

            Assert.IsNull(HeatRecordReader.CounterInitialization(record));
            Assert.AreEqual(0, HeatRecordReader.CounterCommits(record).Count);
            Assert.AreEqual(0, record.CounterHistory.Count);
            Assert.AreEqual(0, HeatRecordReader.CountSubtype(record, ExecutionEventSubtypes.ProcessRuleIntervened));
            Assert.AreEqual(
                16,
                record.FinalState.FinalValue.Value,
                "Without a rule, every multiplication resolves: 1 → 2 → 4 → 8 → 16.");
        }

        [Test]
        public void CounterHistory_HoldsEveryPieceOfCounterEvidence()
        {
            Assert.AreEqual(
                HeatRecordReader.CounterEvidence(_ladder).Count,
                _ladder.CounterHistory.Count,
                "The counter history is derived from the Process-counter payloads, whatever their subtype.");
        }

        [Test]
        public void CounterEvidence_DecomposesIntoInitializationCommitsAndInterventions()
        {
            int interventions = HeatRecordReader.CountSubtype(_ladder, ExecutionEventSubtypes.ProcessRuleIntervened);

            Assert.AreEqual(1, interventions);
            Assert.AreEqual(3, HeatRecordReader.CounterCommits(_ladder).Count);
            Assert.AreEqual(
                1 + 3 + interventions,
                _ladder.CounterHistory.Count,
                "One reset boundary, three committed gains, and the pre-check that reported the counter without changing it.");
        }

        [Test]
        public void InterventionEvidence_IsReachableThroughTheCounterHistory()
        {
            IReadOnlyList<EventEvidence> evidence = HeatRecordReader.CounterEvidence(_ladder);
            bool carriesIntervention = false;
            for (int i = 0; i < evidence.Count; i++)
            {
                if (evidence[i].Subtype == ExecutionEventSubtypes.ProcessRuleIntervened)
                    carriesIntervention = true;
            }

            Assert.IsTrue(
                carriesIntervention,
                "Counter evidence must expose the intervention result, so the pre-check has to be reachable here.");
        }

        [Test]
        public void CounterCommit_CarriesTheOwningRuleOriginAndItsCause()
        {
            EventEvidence commit = FirstCounterChange(_ladder);

            Assert.AreEqual(new InstanceID(RuleInstance), commit.EffectOriginInstance);
            Assert.IsNotNull(commit.CausingEvent, "A counter change is caused by the operation that requested it.");
            Assert.AreEqual(QuantityOperationType.Increment, ((QuantityChangePayload)commit.Payload).Operation);
        }

        [Test]
        public void HeatSkip_RecordsOneTransformation()
        {
            Assert.AreEqual(
                1,
                _ladder.SafetyCounts.OperationTransformations,
                "The intervention transformed one pending operation.");
        }

        [Test]
        public void RescuedHeatSkip_RecordsASecondTransformation()
        {
            ExecutionRecord record = HeatRecordReader.Execute(LadderRequest(true));

            Assert.AreEqual(
                2,
                record.SafetyCounts.OperationTransformations,
                "The skip and the rescue each transform the same pending operation.");
        }

        [Test]
        public void ModificationCommits_StillDoNotCountAsTransformations()
        {
            ExecutionRecord record = HeatRecordReader.Execute(RuleFreeLadderRequest());

            Assert.AreEqual(
                0,
                record.SafetyCounts.OperationTransformations,
                "Ordinary parameter modification is not a transformation, and no rule intervened.");
        }

        [Test]
        public void ThrottlingStatus_IsDerivableFromTheFinalCounterValue()
        {
            IReadOnlyList<EventEvidence> crossings = HeatRecordReader.HeatCrossings(_ladder);

            Assert.AreEqual(3, HeatRecordReader.FinalHeat(_ladder));
            Assert.AreEqual(
                ExecutionEventSubtypes.ThresholdCrossedUpward,
                crossings[crossings.Count - 1].Subtype,
                "At three, the last crossing recorded is the entry into throttling.");
        }

        [Test]
        public void EveryHeatChange_RemainsIndividuallyReconstructable()
        {
            IReadOnlyList<QuantityChangePayload> commits = HeatRecordReader.CounterCommits(_ladder);
            int running = 0;

            for (int i = 0; i < commits.Count; i++)
            {
                Assert.AreEqual(running, commits[i].PriorValue, "Each change begins where the previous one landed.");
                running = commits[i].FinalValue;
            }

            Assert.AreEqual(3, running);
            Assert.AreEqual(HeatRecordReader.FinalHeat(_ladder), running);
        }

        [Test]
        public void ThrottlingLadder_IsDeterministicAcrossTwoRuns()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            ExecutionRequest request = LadderRequest(true);

            ExecutionRecord first = scheduler.Execute(request);
            ExecutionRecord second = scheduler.Execute(request);

            Assert.AreEqual(ExecutionCompletionStatus.Completed, first.CompletionStatus);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void ThrottledExecution_StaysNormalAndValid()
        {
            Assert.AreEqual(SafetyStatus.Normal, _ladder.SafetyStatus);
            Assert.AreEqual(ResultValidityStatus.Valid, _ladder.ResultValidity);
            Assert.AreEqual(ExecutionCompletionStatus.Completed, _ladder.CompletionStatus);
            Assert.AreEqual(0, HeatRecordReader.CountSubtype(_ladder, ExecutionEventSubtypes.SafetyLimitApproached));
        }

        /// <summary>
        /// A ladder of one <c>Value += 1</c> followed by four <c>Value ×= 2</c> player Instructions,
        /// under THERMAL THROTTLE, with SAFE MODE installed only when a rescue is wanted.
        /// </summary>
        /// <param name="withSafeMode">Whether to install the rescuing Dependency.</param>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest LadderRequest(bool withSafeMode)
        {
            List<DependencyInstance> installed = new List<DependencyInstance>();
            if (withSafeMode)
                installed.Add(SchedulerFixtures.SafeModeInstance(60));

            return SchedulerFixtures.RequestOver(
                LadderArrangement(),
                SchedulerFixtures.ZeroState(),
                SchedulerFixtures.ConfigurationWithRule(SchedulerFixtures.ThermalThrottleRule(RuleInstance), null),
                installed,
                new List<DirectiveInstance>());
        }

        /// <summary>
        /// The same ladder under the standard configuration, which declares no Process rule.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest RuleFreeLadderRequest()
        {
            return SchedulerFixtures.RequestOver(
                LadderArrangement(),
                SchedulerFixtures.ZeroState(),
                SchedulerFixtures.StandardConfiguration(),
                new List<DependencyInstance>(),
                new List<DirectiveInstance>());
        }

        /// <summary>
        /// The shared five-slot ladder arrangement.
        /// </summary>
        /// <returns>The source arrangement.</returns>
        private static SourceArrangement LadderArrangement()
        {
            return new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForInstruction(new SourcePosition(1), SchedulerFixtures.ValueAddInstance(50, 1)),
                SourceSlot.ForInstruction(new SourcePosition(2), SchedulerFixtures.ValueMultiplyInstance(51, 2)),
                SourceSlot.ForInstruction(new SourcePosition(3), SchedulerFixtures.ValueMultiplyInstance(52, 2)),
                SourceSlot.ForInstruction(new SourcePosition(4), SchedulerFixtures.ValueMultiplyInstance(53, 2)),
                SourceSlot.ForInstruction(new SourcePosition(5), SchedulerFixtures.ValueMultiplyInstance(54, 2))
            });
        }

        /// <summary>
        /// Returns the first Process-counter change event, failing when none exists.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <returns>The first counter-change event.</returns>
        private static EventEvidence FirstCounterChange(ExecutionRecord record)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype != ExecutionEventSubtypes.QuantityChanged)
                    continue;

                if (evidence.Payload is QuantityChangePayload { Category: QuantityCategory.ProcessCounter })
                    return evidence;
            }

            Assert.Fail("No Process-counter change was recorded.");

            return null;
        }

        /// <summary>
        /// Returns the index of the Process-counter initialization event, or minus one when absent.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <returns>The event index, or minus one.</returns>
        private static int FirstIndexOfCounterReset(ExecutionRecord record)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype != ExecutionEventSubtypes.QuantityReset)
                    continue;

                if (evidence.Payload is QuantityChangePayload { Category: QuantityCategory.ProcessCounter })
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Returns the index of the first event carrying the given subtype.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="subtype">The subtype token.</param>
        /// <returns>The event index.</returns>
        private static int FirstIndexOfSubtype(ExecutionRecord record, string subtype)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                if (record.Events[i].Evidence.Subtype == subtype)
                    return i;
            }

            Assert.Fail("No " + subtype + " event was recorded.");

            return -1;
        }
    }
}
