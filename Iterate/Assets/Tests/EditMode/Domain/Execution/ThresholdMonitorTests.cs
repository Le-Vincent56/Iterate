using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Execution;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the pure <see cref="ThresholdMonitor"/> against the §28.3 bands (Pass 20, Optimize 30,
    /// Benchmark 36): upward crossings on <c>prior &lt; t ≤ final</c>, downward on <c>prior ≥ t &gt;
    /// final</c>, ordered lowest→highest upward and highest→lowest downward, one crossing each, with no
    /// crossing when the side is unchanged.
    /// </summary>
    public sealed class ThresholdMonitorTests
    {
        private static ProcessThresholds Standard()
        {
            return new ProcessThresholds(new ScoreValue(20), new ScoreValue(30), new ScoreValue(36));
        }

        [Test]
        public void Collect_UpwardBelowPass_Empty()
        {
            IReadOnlyList<ThresholdCrossing> crossings = ThresholdMonitor.Collect(Standard(), 7, 12);

            Assert.AreEqual(0, crossings.Count);
        }

        [Test]
        public void Collect_UpwardExactBoundary_SingleUpwardPassCrossing()
        {
            IReadOnlyList<ThresholdCrossing> crossings = ThresholdMonitor.Collect(Standard(), 19, 20);

            Assert.AreEqual(1, crossings.Count);
            Assert.AreEqual(ProcessThresholds.PassName, crossings[0].ThresholdName);
            Assert.AreEqual(new ScoreValue(20), crossings[0].Threshold);
            Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedUpward, crossings[0].Subtype);
        }

        [Test]
        public void Collect_UpwardStartingAtThreshold_Empty()
        {
            IReadOnlyList<ThresholdCrossing> crossings = ThresholdMonitor.Collect(Standard(), 20, 25);

            Assert.AreEqual(0, crossings.Count);
        }

        [Test]
        public void Collect_UpwardCrossesPassOnly()
        {
            IReadOnlyList<ThresholdCrossing> crossings = ThresholdMonitor.Collect(Standard(), 14, 24);

            Assert.AreEqual(1, crossings.Count);
            Assert.AreEqual(ProcessThresholds.PassName, crossings[0].ThresholdName);
        }

        [Test]
        public void Collect_UpwardCrossesAllThree_OrderedLowToHigh()
        {
            IReadOnlyList<ThresholdCrossing> crossings = ThresholdMonitor.Collect(Standard(), 18, 36);

            Assert.AreEqual(3, crossings.Count);
            Assert.AreEqual(ProcessThresholds.PassName, crossings[0].ThresholdName);
            Assert.AreEqual(ProcessThresholds.OptimizeName, crossings[1].ThresholdName);
            Assert.AreEqual(ProcessThresholds.BenchmarkName, crossings[2].ThresholdName);
            Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedUpward, crossings[0].Subtype);
            Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedUpward, crossings[2].Subtype);
        }

        [Test]
        public void Collect_DownwardCrossesAllThree_OrderedHighToLow()
        {
            IReadOnlyList<ThresholdCrossing> crossings = ThresholdMonitor.Collect(Standard(), 36, 0);

            Assert.AreEqual(3, crossings.Count);
            Assert.AreEqual(ProcessThresholds.BenchmarkName, crossings[0].ThresholdName);
            Assert.AreEqual(ProcessThresholds.OptimizeName, crossings[1].ThresholdName);
            Assert.AreEqual(ProcessThresholds.PassName, crossings[2].ThresholdName);
            Assert.AreEqual(ExecutionEventSubtypes.ThresholdCrossedDownward, crossings[0].Subtype);
        }

        [Test]
        public void Collect_DownwardEndingAtThreshold_Empty()
        {
            IReadOnlyList<ThresholdCrossing> crossings = ThresholdMonitor.Collect(Standard(), 25, 20);

            Assert.AreEqual(0, crossings.Count);
        }

        [Test]
        public void Collect_NoChange_Empty()
        {
            IReadOnlyList<ThresholdCrossing> crossings = ThresholdMonitor.Collect(Standard(), 30, 30);

            Assert.AreEqual(0, crossings.Count);
        }

        [Test]
        public void Collect_NullThresholds_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = ThresholdMonitor.Collect(null, 0, 25));
        }
    }
}
