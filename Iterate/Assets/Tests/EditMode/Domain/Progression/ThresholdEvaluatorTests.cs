using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Execution.Tests;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests the tier a Process's final output reaches. Only the designated final output decides it,
    /// and an invalid result reaches no tier however high that output is — a safety-aborted execution
    /// produces a number, and rewarding it would pay out for a run the engine could not complete.
    /// </summary>
    public sealed class ThresholdEvaluatorTests
    {
        private static readonly ProcessThresholdSpec _thresholds = new(10, 30, 60);

        [Test]
        public void Evaluate_BelowPass_ReachesNoTier()
        {
            ThresholdEvaluation evaluation = Evaluate(9);

            Assert.IsTrue(evaluation.Succeeded);
            Assert.IsNull(evaluation.Reached);
        }

        [Test]
        public void Evaluate_AtPass_ReachesPass()
        {
            Assert.AreEqual(RewardTier.Pass, Evaluate(10).Reached);
        }

        [Test]
        public void Evaluate_BetweenPassAndOptimize_StaysAtPass()
        {
            Assert.AreEqual(RewardTier.Pass, Evaluate(29).Reached);
        }

        [Test]
        public void Evaluate_AtOptimize_ReachesOptimize()
        {
            Assert.AreEqual(RewardTier.Optimize, Evaluate(30).Reached);
        }

        [Test]
        public void Evaluate_BetweenOptimizeAndBenchmark_StaysAtOptimize()
        {
            Assert.AreEqual(RewardTier.Optimize, Evaluate(59).Reached);
        }

        [Test]
        public void Evaluate_AtBenchmark_ReachesBenchmark()
        {
            Assert.AreEqual(RewardTier.Benchmark, Evaluate(60).Reached);
        }

        [Test]
        public void Evaluate_BeyondBenchmark_StaysAtBenchmark()
        {
            Assert.AreEqual(RewardTier.Benchmark, Evaluate(600).Reached);
        }

        [Test]
        public void Evaluate_AnInvalidResult_RejectsRegardlessOfOutput()
        {
            ThresholdEvaluation evaluation = ThresholdEvaluator.Evaluate(
                State(600),
                ResultValidityStatus.Invalid,
                _thresholds);

            Assert.IsFalse(evaluation.Succeeded);
            Assert.AreEqual(ThresholdRejection.ResultInvalid, evaluation.Rejection);
            Assert.IsNull(evaluation.Reached);
        }

        [Test]
        public void Evaluate_ReadsTheFinalOutputRatherThanTheScoreRegister()
        {
            FinalExecutionState state = new(
                new ValueAmount(0),
                new SignalValue(0),
                new ScoreValue(600),
                new ScoreValue(9));

            ThresholdEvaluation evaluation = ThresholdEvaluator.Evaluate(state, ResultValidityStatus.Valid, _thresholds);

            Assert.IsNull(evaluation.Reached);
        }

        [Test]
        public void EvaluateRecord_AgreesWithTheValueOverloadOnARealExecution()
        {
            ExecutionRecord record = Execute(SchedulerFixtures.StrongOrderRequest(
                SchedulerFixtures.ZeroState(),
                new InstanceIDSource()));
            ProcessThresholdSpec thresholds = new(1, 2, 3);

            ThresholdEvaluation fromRecord = ThresholdEvaluator.Evaluate(record, thresholds);
            ThresholdEvaluation fromValues = ThresholdEvaluator.Evaluate(
                record.FinalState,
                record.ResultValidity,
                thresholds);

            Assert.AreEqual(ResultValidityStatus.Valid, record.ResultValidity);
            Assert.AreEqual(fromValues.Succeeded, fromRecord.Succeeded);
            Assert.AreEqual(fromValues.Reached, fromRecord.Reached);
            Assert.AreEqual(fromValues.Rejection, fromRecord.Rejection);
        }

        [Test]
        public void EvaluateRecord_ASafetyAbortedExecution_RejectsResultInvalid()
        {
            StructureInstance structure = SchedulerFixtures.RepeatStructure(
                50,
                SafetyCeilings.SourceExecutionUnitsPerExecution + 1,
                2);
            SourceArrangement arrangement = new SourceArrangement(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), structure, SchedulerFixtures.SignalAddInstance(51, 1))
            });
            ExecutionRecord record = Execute(SchedulerFixtures.RequestOver(
                arrangement,
                SchedulerFixtures.ZeroState(),
                new InstanceIDSource()));

            ThresholdEvaluation evaluation = ThresholdEvaluator.Evaluate(record, new ProcessThresholdSpec(1, 2, 3));

            Assert.AreEqual(ExecutionCompletionStatus.SafetyAborted, record.CompletionStatus);
            Assert.IsFalse(evaluation.Succeeded);
            Assert.AreEqual(ThresholdRejection.ResultInvalid, evaluation.Rejection);
            Assert.IsNull(evaluation.Reached);
        }

        private static ExecutionRecord Execute(ExecutionRequest request)
        {
            return new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
        }

        private static ThresholdEvaluation Evaluate(int output)
        {
            return ThresholdEvaluator.Evaluate(State(output), ResultValidityStatus.Valid, _thresholds);
        }

        private static FinalExecutionState State(int output)
        {
            return new FinalExecutionState(
                new ValueAmount(0),
                new SignalValue(0),
                new ScoreValue(output),
                new ScoreValue(output));
        }
    }
}
