using NUnit.Framework;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests that the safety and intervention event-subtype constants on
    /// <see cref="ExecutionEventSubtypes"/> hold the CAB tokens verbatim (§7.19 safety family,
    /// §7.14 intervention family), and that they pair with the already-declared
    /// <see cref="EventFamilies"/> names the abort and Process-rule evidence is emitted under.
    /// </summary>
    public sealed class SafetyEventSubtypeTests
    {
        [Test]
        public void SafetyTokens_AreCabVerbatim()
        {
            Assert.AreEqual("SAFETY_LIMIT_APPROACHED", ExecutionEventSubtypes.SafetyLimitApproached);
            Assert.AreEqual("SAFETY_LIMIT_REACHED", ExecutionEventSubtypes.SafetyLimitReached);
            Assert.AreEqual("CAUSAL_BRANCH_TERMINATED", ExecutionEventSubtypes.CausalBranchTerminated);
            Assert.AreEqual("EXECUTION_SAFETY_ABORTED", ExecutionEventSubtypes.ExecutionSafetyAborted);
        }

        [Test]
        public void ProcessRuleInterventionToken_IsCabVerbatim()
        {
            Assert.AreEqual("PROCESS_RULE_INTERVENED", ExecutionEventSubtypes.ProcessRuleIntervened);
        }

        [Test]
        public void SafetyTokens_EmitUnderTheSafetyFamily()
        {
            Assert.AreEqual("SAFETY", EventFamilies.Safety);
        }

        [Test]
        public void ProcessRuleInterventionToken_EmitsUnderTheInterventionFamily()
        {
            Assert.AreEqual("INTERVENTION", EventFamilies.Intervention);
        }
    }
}
