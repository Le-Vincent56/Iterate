using System;
using NUnit.Framework;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests that <see cref="ExecutionSafetyTallies"/> begins at zero, counts opened units, projects to a
    /// structurally-zero <see cref="SafetyCounts"/> save the unit count, and enforces the source-execution
    /// ceiling as preflight-permits-reaching / prohibits-exceeding.
    /// </summary>
    public sealed class ExecutionSafetyTalliesTests
    {
        [Test]
        public void Fresh_ZeroUnits_AllZeroCounts()
        {
            ExecutionSafetyTallies tallies = new();

            Assert.AreEqual(0, tallies.SourceExecutionUnits);
            Assert.AreEqual(new SafetyCounts(0, 0, 0, 0, 0), tallies.ToCounts());
        }

        [Test]
        public void RecordUnitOpened_ThreeTimes_CountsThree()
        {
            ExecutionSafetyTallies tallies = new();

            tallies.RecordUnitOpened();
            tallies.RecordUnitOpened();
            tallies.RecordUnitOpened();

            Assert.AreEqual(3, tallies.SourceExecutionUnits);
            Assert.AreEqual(new SafetyCounts(0, 0, 3, 0, 0), tallies.ToCounts());
        }

        [Test]
        public void PreflightUnitOpening_TrueAtZeroAndAfterSevenOpens()
        {
            ExecutionSafetyTallies tallies = new();

            Assert.IsTrue(tallies.PreflightUnitOpening());

            for (int i = 0; i < 7; i++)
                tallies.RecordUnitOpened();

            Assert.IsTrue(tallies.PreflightUnitOpening());
        }

        [Test]
        public void RecordUnitOpened_ReachingCeiling_IsLegalThenPreflightFalse()
        {
            ExecutionSafetyTallies tallies = new();

            for (int i = 0; i < SafetyCeilings.SourceExecutionUnitsPerExecution - 1; i++)
                tallies.RecordUnitOpened();

            Assert.IsTrue(tallies.PreflightUnitOpening());
            Assert.DoesNotThrow(() => tallies.RecordUnitOpened());
            Assert.AreEqual(SafetyCeilings.SourceExecutionUnitsPerExecution, tallies.SourceExecutionUnits);
            Assert.IsFalse(tallies.PreflightUnitOpening());
        }

        [Test]
        public void RecordUnitOpened_BeyondCeiling_Throws()
        {
            ExecutionSafetyTallies tallies = new();

            for (int i = 0; i < SafetyCeilings.SourceExecutionUnitsPerExecution; i++)
                tallies.RecordUnitOpened();

            Assert.Throws<InvalidOperationException>(() => tallies.RecordUnitOpened());
            Assert.AreEqual(SafetyCeilings.SourceExecutionUnitsPerExecution, tallies.SourceExecutionUnits);
        }
    }
}
