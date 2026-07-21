using NUnit.Framework;
using Iterate.Domain.Execution;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests that <see cref="ExecutionEventSubtypes"/> holds the seven CAB event-subtype tokens verbatim,
    /// pinning the exact canonical strings so evidence never derives them from enum names.
    /// </summary>
    public sealed class ExecutionEventSubtypesTests
    {
        [Test]
        public void Tokens_AreCabVerbatim()
        {
            Assert.AreEqual("EXECUTION_STARTED", ExecutionEventSubtypes.ExecutionStarted);
            Assert.AreEqual("EXECUTION_COMPLETED", ExecutionEventSubtypes.ExecutionCompleted);
            Assert.AreEqual("QUANTITY_RESET", ExecutionEventSubtypes.QuantityReset);
            Assert.AreEqual("QUANTITY_ASSIGNED", ExecutionEventSubtypes.QuantityAssigned);
            Assert.AreEqual("QUANTITY_CHANGED", ExecutionEventSubtypes.QuantityChanged);
            Assert.AreEqual("THRESHOLD_CROSSED_UPWARD", ExecutionEventSubtypes.ThresholdCrossedUpward);
            Assert.AreEqual("THRESHOLD_CROSSED_DOWNWARD", ExecutionEventSubtypes.ThresholdCrossedDownward);
        }
    }
}
