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
    /// The cooling half of the CAB-EVT-753 conformance matrix: primary Score cooling, separate
    /// OUTPUT CACHE cooling, cooling clamped at zero, the cooling window that ends at the designated
    /// final Core output, and cooling from three to two permitting a later multiplication.
    /// The window fixture mirrors the shape of the Design §28.9 Core rather than reproducing it — an
    /// intermediate Core output that cools, then a further multiplication, then the designated final
    /// output that does not. Positions 5 and 7 stand in for that Core's lines 10 and 12; the shipped
    /// Core itself is driven at SHA-07.
    /// Cooling never cascades. A committed counter change writes no register and is offered to no
    /// boundary, so the only way to reach a second cooling request is a second eligible Score change.
    /// </summary>
    public sealed class ExecutionSchedulerHeatCoolingTests
    {
        /// <summary>
        /// The Process-rule instance identity every fixture in this suite configures.
        /// </summary>
        private const int RuleInstance = 80;

        /// <summary>
        /// The position of the designated final Core output in the window fixture.
        /// </summary>
        private const int DesignatedOutputPosition = 7;

        /// <summary>
        /// The record of the seven-slot window fixture: warm to three, cool at the intermediate
        /// output, multiply again, then score past the closed window.
        /// </summary>
        private ExecutionRecord _window;

        /// <summary>
        /// Runs the shared window fixture once for the whole suite.
        /// </summary>
        [OneTimeSetUp]
        public void RunSharedFixture()
        {
            _window = HeatRecordReader.Execute(WindowRequest());
        }

        [Test]
        public void PositiveScoreChange_CoolsHeatByOne()
        {
            IReadOnlyList<QuantityChangePayload> commits = HeatRecordReader.CounterCommits(_window);

            Assert.AreEqual(5, commits.Count, "Four gains and one cooling; the final output cools nothing.");
            HeatRecordReader.AssertCooling(commits[3], 3, 2);
        }

        [Test]
        public void CoolingResolvesAfterTheCausingScoreChange()
        {
            IReadOnlyList<QuantityChangePayload> commits = HeatRecordReader.CounterCommits(_window);

            Assert.AreEqual(2, commits[3].FinalValue, "The intermediate output's cooling landed Heat at two.");
            Assert.AreEqual(8, ScoreChangeAt(_window, new SourcePosition(5)).FinalValue, "Score resolved to 8 first.");
        }

        [Test]
        public void CoolingFromThreeToTwo_PermitsTheNextMultiplication()
        {
            EventEvidence finalized = HeatRecordReader.DispositionAt(_window, new SourcePosition(6));
            IReadOnlyList<QuantityChangePayload> commits = HeatRecordReader.CounterCommits(_window);

            Assert.AreEqual(EventDisposition.Resolved, finalized.Disposition);
            Assert.AreEqual(0, HeatRecordReader.CountSubtype(_window, ExecutionEventSubtypes.ProcessRuleIntervened));
            HeatRecordReader.AssertGain(commits[4], 2, 3);
            Assert.AreEqual(16, _window.FinalState.FinalValue.Value, "The permitted multiplication doubled 8 to 16.");
        }

        [Test]
        public void DesignatedFinalCoreOutput_DoesNotCool()
        {
            IReadOnlyList<QuantityChangePayload> commits = HeatRecordReader.CounterCommits(_window);

            Assert.AreEqual(
                24,
                ScoreChangeAt(_window, new SourcePosition(DesignatedOutputPosition)).FinalValue,
                "The final output scored 8 + 16 = 24.");
            Assert.AreEqual(3, commits[commits.Count - 1].FinalValue, "Heat is still three after the closed window.");
            Assert.AreEqual(3, HeatRecordReader.FinalHeat(_window));
        }

        [Test]
        public void RepeatedThrottlingCrossings_StaySeparate()
        {
            IReadOnlyList<EventEvidence> crossings = HeatRecordReader.HeatCrossings(_window);

            Assert.AreEqual(3, crossings.Count, "Entry, exit, and re-entry are three distinct crossings.");
            Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedUpward, crossings[0].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedDownward, crossings[1].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedUpward, crossings[2].Subtype);
        }

        [Test]
        public void OutputCacheScoreChange_CoolsSeparatelyFromItsCausingChange()
        {
            ExecutionRecord record = HeatRecordReader.Execute(OutputCacheRequest());
            IReadOnlyList<QuantityChangePayload> commits = HeatRecordReader.CounterCommits(record);

            Assert.AreEqual(4, commits.Count, "Two gains, then one cooling per eligible Score change.");
            HeatRecordReader.AssertCooling(commits[2], 2, 1);
            HeatRecordReader.AssertCooling(commits[3], 1, 0);
            Assert.AreEqual(5, record.FinalState.FinalScore.Value, "Score 4 from the Instruction plus 1 from the reaction.");
            Assert.AreEqual(0, HeatRecordReader.FinalHeat(record));
        }

        [Test]
        public void CoolingAtZero_ResolvesWithZeroDeltaAndBoundEvidence()
        {
            ExecutionRecord record = HeatRecordReader.Execute(ColdScoringRequest());
            IReadOnlyList<QuantityChangePayload> commits = HeatRecordReader.CounterCommits(record);

            Assert.AreEqual(1, commits.Count);
            Assert.AreEqual(0, commits[0].PriorValue);
            Assert.AreEqual(-1, commits[0].RequestedAmount);
            Assert.AreEqual(0, commits[0].FinalDelta);
            Assert.AreEqual(0, commits[0].FinalValue);
            Assert.IsNotNull(commits[0].AppliedBounds, "The minimum bound applied, and that is the clamp evidence.");
            Assert.AreEqual(0, commits[0].AppliedBounds.Minimum);
        }

        [Test]
        public void ZeroDeltaScoreChange_DoesNotCool()
        {
            ExecutionRecord record = HeatRecordReader.Execute(ZeroScoreRequest());

            Assert.AreEqual(0, record.FinalState.FinalScore.Value);
            Assert.AreEqual(
                0,
                HeatRecordReader.CounterCommits(record).Count,
                "A zero actual delta fails the positive-sign qualifier, so nothing cools.");
        }

        [Test]
        public void ThrottlingWindowFixture_IsDeterministicAcrossTwoRuns()
        {
            ExecutionScheduler scheduler = new ExecutionScheduler(new ExecutionTraceBuilder());
            ExecutionRequest request = WindowRequest();

            ExecutionRecord first = scheduler.Execute(request);
            ExecutionRecord second = scheduler.Execute(request);

            Assert.AreEqual(ExecutionCompletionStatus.Completed, first.CompletionStatus);
            Assert.AreEqual(first, second);
        }

        /// <summary>
        /// The window fixture: <c>Value += 1</c>, three Core multiplications that carry Heat to three,
        /// an intermediate Core output that scores and cools, a further multiplication the cooling
        /// permits, and the designated final Core output whose activation has already closed the
        /// window.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest WindowRequest()
        {
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), SchedulerFixtures.ValueCoreLine("L01", CoreLineOperator.Add, 1)),
                SourceSlot.ForCore(new SourcePosition(2), SchedulerFixtures.ValueCoreLine("L02", CoreLineOperator.Multiply, 2)),
                SourceSlot.ForCore(new SourcePosition(3), SchedulerFixtures.ValueCoreLine("L03", CoreLineOperator.Multiply, 2)),
                SourceSlot.ForCore(new SourcePosition(4), SchedulerFixtures.ValueCoreLine("L04", CoreLineOperator.Multiply, 2)),
                SourceSlot.ForCore(new SourcePosition(5), SchedulerFixtures.ScoreOutputCoreLine("L05")),
                SourceSlot.ForCore(new SourcePosition(6), SchedulerFixtures.ValueCoreLine("L06", CoreLineOperator.Multiply, 2)),
                SourceSlot.ForCore(new SourcePosition(DesignatedOutputPosition), SchedulerFixtures.ScoreOutputCoreLine("L07"))
            });

            return RequestOverArrangement(
                arrangement,
                new SourcePosition(DesignatedOutputPosition),
                new List<DirectiveInstance>());
        }

        /// <summary>
        /// Two Core multiplications warm Heat to two, then one player <c>Score += Value</c>
        /// Instruction scores under an OUTPUT-CACHE-shaped reaction that scores separately. Both
        /// positive Score changes are eligible, so each creates its own cooling request.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest OutputCacheRequest()
        {
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), SchedulerFixtures.ValueCoreLine("L01", CoreLineOperator.Add, 1)),
                SourceSlot.ForCore(new SourcePosition(2), SchedulerFixtures.ValueCoreLine("L02", CoreLineOperator.Multiply, 2)),
                SourceSlot.ForCore(new SourcePosition(3), SchedulerFixtures.ValueCoreLine("L03", CoreLineOperator.Multiply, 2)),
                SourceSlot.ForInstruction(new SourcePosition(4), SchedulerFixtures.ScoreAddValueInstance(58))
            });

            return RequestOverArrangement(
                arrangement,
                null,
                new List<DirectiveInstance> { SchedulerFixtures.ScoreGainReactionPragma(75, 1) });
        }

        /// <summary>
        /// One <c>Value += 1</c> and one Core output, so the single eligible Score change requests
        /// cooling while Heat is still zero.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest ColdScoringRequest()
        {
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), SchedulerFixtures.ValueCoreLine("L01", CoreLineOperator.Add, 1)),
                SourceSlot.ForCore(new SourcePosition(2), SchedulerFixtures.ScoreOutputCoreLine("L02"))
            });

            return RequestOverArrangement(arrangement, null, new List<DirectiveInstance>());
        }

        /// <summary>
        /// A Core output with Value still zero, so the Score change resolves with a zero actual delta.
        /// </summary>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest ZeroScoreRequest()
        {
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForCore(new SourcePosition(1), SchedulerFixtures.ScoreOutputCoreLine("L01"))
            });

            return RequestOverArrangement(arrangement, null, new List<DirectiveInstance>());
        }

        /// <summary>
        /// Assembles a request over the arrangement under THERMAL THROTTLE with the given designated
        /// final Core output and pragmas, and no installed Dependencies.
        /// </summary>
        /// <param name="arrangement">The source arrangement.</param>
        /// <param name="designatedOutput">The designated final Core output position, or null.</param>
        /// <param name="pragmas">The active Directive pragmas.</param>
        /// <returns>The assembled request.</returns>
        private static ExecutionRequest RequestOverArrangement(
            SourceArrangement arrangement,
            SourcePosition? designatedOutput,
            List<DirectiveInstance> pragmas)
        {
            return SchedulerFixtures.RequestOver(
                arrangement,
                SchedulerFixtures.ZeroState(),
                SchedulerFixtures.ConfigurationWithRule(SchedulerFixtures.ThermalThrottleRule(RuleInstance), designatedOutput),
                new List<DependencyInstance>(),
                pragmas);
        }

        /// <summary>
        /// Returns the runtime-variable Score change committed by the unit at the given position.
        /// </summary>
        /// <param name="record">The frozen record.</param>
        /// <param name="position">The source position.</param>
        /// <returns>The Score change payload.</returns>
        private static QuantityChangePayload ScoreChangeAt(ExecutionRecord record, SourcePosition position)
        {
            for (int i = 0; i < record.Events.Count; i++)
            {
                EventEvidence evidence = record.Events[i].Evidence;
                if (evidence.Subtype != ExecutionEventSubtypes.QuantityChanged || evidence.Position != position)
                    continue;

                if (evidence.Payload is QuantityChangePayload { Category: QuantityCategory.RuntimeVariable } payload)
                    return payload;
            }

            Assert.Fail("No runtime-variable quantity change at position " + position.LineNumber + ".");

            return null;
        }
    }
}
