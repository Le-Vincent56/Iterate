using NUnit.Framework;
using Iterate.Domain.Execution;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests that the event-subtype constants on <see cref="ExecutionEventSubtypes"/> hold the CAB
    /// tokens verbatim (§7.5, §7.6, §7.7, §7.8, §7.10, §7.11, §7.12, §7.13), pinning the exact
    /// canonical strings so evidence never derives them.
    /// </summary>
    public sealed class ExecutionEventSubtypeTests
    {
        [Test]
        public void SourceTokens_AreCabVerbatim()
        {
            Assert.AreEqual("SOURCE_EXECUTION_STARTED", ExecutionEventSubtypes.SourceExecutionStarted);
            Assert.AreEqual("SOURCE_EXECUTION_COMPLETED", ExecutionEventSubtypes.SourceExecutionCompleted);
        }

        [Test]
        public void OperationTokens_AreCabVerbatim()
        {
            Assert.AreEqual("PRIMARY_OPERATION_PENDING", ExecutionEventSubtypes.PrimaryOperationPending);
            Assert.AreEqual("PRIMARY_OPERATION_MODIFIED", ExecutionEventSubtypes.PrimaryOperationModified);
            Assert.AreEqual("PRIMARY_OPERATION_RESOLVED", ExecutionEventSubtypes.PrimaryOperationResolved);
            Assert.AreEqual("PRIMARY_OPERATION_RESULT_FINALIZED", ExecutionEventSubtypes.PrimaryOperationResultFinalized);
        }

        [Test]
        public void QualificationTokens_AreCabVerbatim()
        {
            Assert.AreEqual("EFFECT_QUALIFIED", ExecutionEventSubtypes.EffectQualified);
            Assert.AreEqual("EFFECT_FAILED_TO_QUALIFY", ExecutionEventSubtypes.EffectFailedToQualify);
            Assert.AreEqual("EFFECT_COMMITTED", ExecutionEventSubtypes.EffectCommitted);
        }

        [Test]
        public void ReactionToken_IsCabVerbatim()
        {
            Assert.AreEqual("IMMEDIATE_REACTION_RESOLVED", ExecutionEventSubtypes.ImmediateReactionResolved);
        }

        [Test]
        public void SourceStreamTokens_AreCabVerbatim()
        {
            Assert.AreEqual("SOURCE_OBJECT_ACTIVATED", ExecutionEventSubtypes.SourceObjectActivated);
            Assert.AreEqual(
                "SOURCE_EXECUTION_DISPOSITION_FINALIZED",
                ExecutionEventSubtypes.SourceExecutionDispositionFinalized);
        }

        [Test]
        public void StructureTokens_AreCabVerbatim()
        {
            Assert.AreEqual("STRUCTURE_ACTIVATED", ExecutionEventSubtypes.StructureActivated);
            Assert.AreEqual("STRUCTURE_ENTERED", ExecutionEventSubtypes.StructureEntered);
            Assert.AreEqual("STRUCTURE_EXITED", ExecutionEventSubtypes.StructureExited);
            Assert.AreEqual("REPEAT_COUNT_CAPTURED", ExecutionEventSubtypes.RepeatCountCaptured);
            Assert.AreEqual("REPEAT_ITERATION_STARTED", ExecutionEventSubtypes.RepeatIterationStarted);
            Assert.AreEqual("REPEAT_ITERATION_COMPLETED", ExecutionEventSubtypes.RepeatIterationCompleted);
            Assert.AreEqual("CONDITION_SNAPSHOT_CAPTURED", ExecutionEventSubtypes.ConditionSnapshotCaptured);
            Assert.AreEqual("CONDITION_TRUE", ExecutionEventSubtypes.ConditionTrue);
            Assert.AreEqual("CONDITION_FALSE", ExecutionEventSubtypes.ConditionFalse);
        }

        [Test]
        public void DispositionTokens_AreCabVerbatim()
        {
            Assert.AreEqual("SOURCE_EXECUTION_SKIPPED", ExecutionEventSubtypes.SourceExecutionSkipped);
            Assert.AreEqual("SOURCE_EXECUTION_RESCUED", ExecutionEventSubtypes.SourceExecutionRescued);
        }

        [Test]
        public void AddedExecutionTokens_AreCabVerbatim()
        {
            Assert.AreEqual("ADDED_EXECUTION_REQUESTED", ExecutionEventSubtypes.AddedExecutionRequested);
            Assert.AreEqual("ADDED_EXECUTION_STARTED", ExecutionEventSubtypes.AddedExecutionStarted);
            Assert.AreEqual("ADDED_EXECUTION_COMPLETED", ExecutionEventSubtypes.AddedExecutionCompleted);
            Assert.AreEqual("ADDED_EXECUTION_CANCELLED", ExecutionEventSubtypes.AddedExecutionCancelled);
        }

        [Test]
        public void BoundaryEffectTokens_AreCabVerbatim()
        {
            Assert.AreEqual("BOUNDARY_EFFECT_REQUESTED", ExecutionEventSubtypes.BoundaryEffectRequested);
            Assert.AreEqual("BOUNDARY_EFFECT_RESOLVED", ExecutionEventSubtypes.BoundaryEffectResolved);
        }

        [Test]
        public void RuntimeUnitCompletedToken_IsCabVerbatim()
        {
            Assert.AreEqual("RUNTIME_UNIT_COMPLETED", ExecutionEventSubtypes.RuntimeUnitCompleted);
        }
    }
}
