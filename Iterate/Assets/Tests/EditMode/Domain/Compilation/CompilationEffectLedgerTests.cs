using NUnit.Framework;
using Iterate.Domain.Compilation;

namespace Iterate.Domain.Compilation.Tests
{
    /// <summary>
    /// Tests the Process-scoped <see cref="CompilationEffectLedger"/>: a source key is unconsumed until
    /// marked, marking makes it consumed, and unrelated keys stay unconsumed.
    /// </summary>
    public sealed class CompilationEffectLedgerTests
    {
        [Test]
        public void IsConsumed_UnmarkedKey_False()
        {
            CompilationEffectLedger ledger = new CompilationEffectLedger();

            Assert.IsFalse(ledger.IsConsumed("WB-DEP-002:0#1"));
        }

        [Test]
        public void MarkConsumed_ThenIsConsumed_True()
        {
            CompilationEffectLedger ledger = new CompilationEffectLedger();

            ledger.MarkConsumed("WB-DEP-002:0#1");

            Assert.IsTrue(ledger.IsConsumed("WB-DEP-002:0#1"));
        }

        [Test]
        public void IsConsumed_DifferentKey_False()
        {
            CompilationEffectLedger ledger = new CompilationEffectLedger();

            ledger.MarkConsumed("WB-DEP-002:0#1");

            Assert.IsFalse(ledger.IsConsumed("WB-DEP-002:0#2"));
        }
    }
}
