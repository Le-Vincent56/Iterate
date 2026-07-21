using System;
using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The stateless collector of Score-band crossings for one finalized Score change. Upward crossings
    /// (<c>prior &lt; threshold ≤ final</c>) are ordered lowest→highest; downward crossings
    /// (<c>prior ≥ threshold &gt; final</c>) highest→lowest; a change that leaves the side unchanged
    /// yields none. Purity carries the no-re-crossing rule: each call sees only one change's prior/final
    /// pair.
    /// </summary>
    public static class ThresholdMonitor
    {
        /// <summary>
        /// Collects the ordered band crossings a single Score change produces.
        /// </summary>
        /// <param name="thresholds">The declared Score bands.</param>
        /// <param name="priorScore">The Score before the change.</param>
        /// <param name="finalScore">The Score after the change.</param>
        /// <returns>The ordered crossings; empty when no band's side changed.</returns>
        /// <exception cref="ArgumentException">Thrown when the thresholds are null.</exception>
        public static IReadOnlyList<ThresholdCrossing> Collect(ProcessThresholds thresholds, int priorScore, int finalScore)
        {
            if (thresholds == null)
                throw new ArgumentException("Threshold collection requires declared thresholds.", nameof(thresholds));

            List<ThresholdCrossing> crossings = new List<ThresholdCrossing>();
            if (finalScore > priorScore)
            {
                AddUpward(crossings, ProcessThresholds.PassName, thresholds.Pass, priorScore, finalScore);
                AddUpward(crossings, ProcessThresholds.OptimizeName, thresholds.Optimize, priorScore, finalScore);
                AddUpward(crossings, ProcessThresholds.BenchmarkName, thresholds.Benchmark, priorScore, finalScore);
            }
            else if (finalScore < priorScore)
            {
                AddDownward(crossings, ProcessThresholds.BenchmarkName, thresholds.Benchmark, priorScore, finalScore);
                AddDownward(crossings, ProcessThresholds.OptimizeName, thresholds.Optimize, priorScore, finalScore);
                AddDownward(crossings, ProcessThresholds.PassName, thresholds.Pass, priorScore, finalScore);
            }

            return crossings;
        }

        /// <summary>
        /// Adds an upward crossing when the band sits in <c>prior &lt; threshold ≤ final</c>.
        /// </summary>
        /// <param name="crossings">The accumulating crossing list.</param>
        /// <param name="name">The canonical band name.</param>
        /// <param name="threshold">The band value.</param>
        /// <param name="priorScore">The Score before the change.</param>
        /// <param name="finalScore">The Score after the change.</param>
        private static void AddUpward(
            List<ThresholdCrossing> crossings,
            string name,
            ScoreValue threshold,
            int priorScore,
            int finalScore
        )
        {
            int value = threshold.Value;
            if (priorScore < value && value <= finalScore)
                crossings.Add(new ThresholdCrossing(name, threshold, ExecutionEventSubtypes.ThresholdCrossedUpward));
        }

        /// <summary>
        /// Adds a downward crossing when the band sits in <c>prior ≥ threshold &gt; final</c>.
        /// </summary>
        /// <param name="crossings">The accumulating crossing list.</param>
        /// <param name="name">The canonical band name.</param>
        /// <param name="threshold">The band value.</param>
        /// <param name="priorScore">The Score before the change.</param>
        /// <param name="finalScore">The Score after the change.</param>
        private static void AddDownward(
            List<ThresholdCrossing> crossings,
            string name,
            ScoreValue threshold,
            int priorScore,
            int finalScore
        )
        {
            int value = threshold.Value;
            if (priorScore >= value && value > finalScore)
                crossings.Add(new ThresholdCrossing(name, threshold, ExecutionEventSubtypes.ThresholdCrossedDownward));
        }
    }
}