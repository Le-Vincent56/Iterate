using System;
using NUnit.Framework;
using Iterate.Domain.Execution;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests that <see cref="ProcessThresholds"/> requires strictly ascending Pass &lt; Optimize &lt;
    /// Benchmark bands, round-trips them, and single-sources the canonical band-name constants.
    /// </summary>
    public sealed class ProcessThresholdsTests
    {
        [Test]
        public void Constructor_StrictlyAscending_RoundTrips()
        {
            ProcessThresholds thresholds = new(new ScoreValue(20), new ScoreValue(30), new ScoreValue(36));

            Assert.AreEqual(new ScoreValue(20), thresholds.Pass);
            Assert.AreEqual(new ScoreValue(30), thresholds.Optimize);
            Assert.AreEqual(new ScoreValue(36), thresholds.Benchmark);
        }

        [Test]
        public void Constructor_PassEqualsOptimize_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ProcessThresholds(new ScoreValue(30), new ScoreValue(30), new ScoreValue(36)));
        }

        [Test]
        public void Constructor_OptimizeBelowPass_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ProcessThresholds(new ScoreValue(30), new ScoreValue(20), new ScoreValue(36)));
        }

        [Test]
        public void Constructor_BenchmarkEqualsOptimize_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ProcessThresholds(new ScoreValue(20), new ScoreValue(30), new ScoreValue(30)));
        }

        [Test]
        public void BandNameConstants_AreCanonicalStrings()
        {
            Assert.AreEqual("Pass", ProcessThresholds.PassName);
            Assert.AreEqual("Optimize", ProcessThresholds.OptimizeName);
            Assert.AreEqual("Benchmark", ProcessThresholds.BenchmarkName);
        }
    }
}
