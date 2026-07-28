using System;
using System.Collections.Generic;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using NUnit.Framework;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Contract tests for <see cref="PredictionInvoker"/>: prediction runs the same engine and returns
    /// the same record a direct execution would, and it is observational — interleaving a prediction
    /// between two live executions leaves both live records byte-identical to a control run that never
    /// predicted. At current content the engine wires no random decisions, so CAB-EVT-900's
    /// "must not consume or advance the actual decision" has no producer to violate; these tests pin
    /// the property that must survive the day one exists, which is the reason the seam is named rather
    /// than inlined at its future call site.
    /// </summary>
    public sealed class PredictionInvokerTests
    {
        [Test]
        public void Prediction_EqualsADirectExecution()
        {
            // Arrange
            ExecutionRequest request = SchedulerFixtures.PassCrossingRequest(new InstanceIDSource());
            ExecutionRecord live = Execute(request);

            // Act
            ExecutionRecord predicted = new PredictionInvoker().Predict(request);

            // Assert — the same engine, the same nine phases, the same record.
            Assert.That(predicted, Is.EqualTo(live));
        }

        [Test]
        public void TwoPredictions_ReturnEqualRecords()
        {
            // Arrange — the same invoker instance, so this also covers its owned builder being reused
            // across calls without carrying state between them.
            ExecutionRequest request = SchedulerFixtures.PassCrossingRequest(new InstanceIDSource());
            PredictionInvoker invoker = new();

            // Act
            ExecutionRecord first = invoker.Predict(request);
            ExecutionRecord second = invoker.Predict(request);

            // Assert
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void PredictionBetweenTwoLiveExecutions_LeavesBothLiveRecordsUnchanged()
        {
            // Arrange — a control pair that never predicts, and a test pair that predicts between its
            // two live executions on the same live scheduler and builder.
            ExecutionRequest first = SchedulerFixtures.PassCrossingRequest(new InstanceIDSource());
            ExecutionRequest second = SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.ZeroState(), new InstanceIDSource());
            ExecutionRequest predicted = SchedulerFixtures.StrongOrderRequest(SchedulerFixtures.State(5, 2, 7), new InstanceIDSource());

            ExecutionScheduler control = new(new ExecutionTraceBuilder());
            ExecutionRecord controlFirst = control.Execute(first);
            ExecutionRecord controlSecond = control.Execute(second);

            ExecutionScheduler live = new(new ExecutionTraceBuilder());

            // Act
            ExecutionRecord liveFirst = live.Execute(first);
            _ = new PredictionInvoker().Predict(predicted);
            ExecutionRecord liveSecond = live.Execute(second);

            // Assert — prediction is observational: it neither advanced nor consumed anything the live
            // sequence depends on, so both live records match their control counterparts exactly.
            Assert.That(liveFirst, Is.EqualTo(controlFirst));
            Assert.That(liveSecond, Is.EqualTo(controlSecond));
        }

        [Test]
        public void AtCeilingRequest_PredictsTheSameSafetyAbortedRecord()
        {
            // Arrange — an execution that breaches the source-execution-unit ceiling. Prediction runs the
            // full nine phases, so it reaches the same abort rather than short-circuiting before it.
            ExecutionRequest request = RepeatRequest(SafetyCeilings.SourceExecutionUnitsPerExecution + 1);
            ExecutionRecord live = Execute(request);

            // Act
            ExecutionRecord predicted = new PredictionInvoker().Predict(request);

            // Assert
            Assert.That(live.CompletionStatus, Is.EqualTo(ExecutionCompletionStatus.SafetyAborted));
            Assert.That(predicted.CompletionStatus, Is.EqualTo(ExecutionCompletionStatus.SafetyAborted));
            Assert.That(predicted.SafetyStatus, Is.EqualTo(SafetyStatus.SafetyAborted));
            Assert.That(predicted, Is.EqualTo(live));
        }

        [Test]
        public void NullRequest_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new PredictionInvoker().Predict(null));
        }

        private static ExecutionRecord Execute(ExecutionRequest request)
        {
            return new ExecutionScheduler(new ExecutionTraceBuilder()).Execute(request);
        }

        private static ExecutionRequest RepeatRequest(int count)
        {
            StructureInstance structure = SchedulerFixtures.RepeatStructure(50, count, 2);
            SourceArrangement arrangement = new(new List<SourceSlot>
            {
                SourceSlot.ForStructureHeader(new SourcePosition(1), structure),
                SourceSlot.ForContainedInstruction(new SourcePosition(2), structure, SchedulerFixtures.SignalAddInstance(51, 1))
            });

            return SchedulerFixtures.RequestOver(arrangement, SchedulerFixtures.ZeroState(), new InstanceIDSource());
        }
    }
}
