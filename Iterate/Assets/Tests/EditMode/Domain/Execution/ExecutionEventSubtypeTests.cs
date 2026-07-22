using NUnit.Framework;
using Iterate.Domain.Execution;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests that the ten effect-system event-subtype constants added to
    /// <see cref="ExecutionEventSubtypes"/> hold the CAB tokens verbatim (§7.6, §7.7, §7.8, §7.12),
    /// pinning the exact canonical strings so evidence never derives them.
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
    }
}
