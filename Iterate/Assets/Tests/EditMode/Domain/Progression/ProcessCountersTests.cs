using NUnit.Framework;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests the Process's execution counters: the authored allowance, how many executions remain, and
    /// the mandatory-execution obligation that decides whether a Process may be left early.
    /// </summary>
    public sealed class ProcessCountersTests
    {
        [Test]
        public void NewCounters_HaveRunNothingAndOweTheWholeAllowance()
        {
            ProcessCounters counters = new(3, false);

            Assert.AreEqual(3, counters.Allowance);
            Assert.AreEqual(0, counters.ExecutionsRun);
            Assert.AreEqual(3, counters.ExecutionsRemaining);
            Assert.IsTrue(counters.CanExecute);
        }

        [Test]
        public void Record_AdvancesTheRunCount()
        {
            ProcessCounters counters = new(3, false);

            CounterResult result = counters.RecordExecution();

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, counters.ExecutionsRun);
            Assert.AreEqual(2, counters.ExecutionsRemaining);
        }

        [Test]
        public void Record_UpToTheAllowance_Succeeds()
        {
            ProcessCounters counters = new(3, false);

            Assert.IsTrue(counters.RecordExecution().Succeeded);
            Assert.IsTrue(counters.RecordExecution().Succeeded);
            Assert.IsTrue(counters.RecordExecution().Succeeded);

            Assert.AreEqual(0, counters.ExecutionsRemaining);
            Assert.IsFalse(counters.CanExecute);
        }

        [Test]
        public void Record_BeyondTheAllowance_IsRejectedAndChangesNothing()
        {
            ProcessCounters counters = new(1, false);
            counters.RecordExecution();

            CounterResult result = counters.RecordExecution();

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(1, counters.ExecutionsRun, "a rejected execution never advances the count.");
        }

        [Test]
        public void WithoutMandatoryExecutions_NothingIsOutstandingAtAnyPoint()
        {
            ProcessCounters counters = new(3, false);

            Assert.AreEqual(0, counters.MandatoryExecutionsRemaining);

            counters.RecordExecution();

            Assert.AreEqual(0, counters.MandatoryExecutionsRemaining);
        }

        [Test]
        public void WithMandatoryExecutions_TheOutstandingCountFallsWithEachExecution()
        {
            ProcessCounters counters = new(4, true);

            Assert.AreEqual(4, counters.MandatoryExecutionsRemaining);

            counters.RecordExecution();
            counters.RecordExecution();

            Assert.AreEqual(2, counters.MandatoryExecutionsRemaining);
        }

        [Test]
        public void WithMandatoryExecutions_TheObligationClearsOnlyAtTheAllowance()
        {
            ProcessCounters counters = new(2, true);
            counters.RecordExecution();

            Assert.AreEqual(1, counters.MandatoryExecutionsRemaining);

            counters.RecordExecution();

            Assert.AreEqual(0, counters.MandatoryExecutionsRemaining);
            Assert.IsFalse(counters.CanExecute);
        }
    }
}
