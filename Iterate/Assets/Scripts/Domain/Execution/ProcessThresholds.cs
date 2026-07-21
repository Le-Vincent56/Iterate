using System;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The three Score bands a Process is judged against, strictly ascending Pass &lt; Optimize &lt;
    /// Benchmark. Also the single source of the canonical band-name strings a threshold-crossing event
    /// uses to name the band it crossed.
    /// </summary>
    /// <param name="Pass">The lowest success band.</param>
    /// <param name="Optimize">The middle success band; strictly above Pass.</param>
    /// <param name="Benchmark">The highest success band; strictly above Optimize.</param>
    public sealed record ProcessThresholds(ScoreValue Pass, ScoreValue Optimize, ScoreValue Benchmark)
    {
        /// <summary>
        /// The canonical name of the Pass band.
        /// </summary>
        public const string PassName = "Pass";

        /// <summary>
        /// The canonical name of the Optimize band.
        /// </summary>
        public const string OptimizeName = "Optimize";

        /// <summary>
        /// The canonical name of the Benchmark band.
        /// </summary>
        public const string BenchmarkName = "Benchmark";

        /// <summary>
        /// The highest success band. Validated strictly above Optimize, itself strictly above Pass, at
        /// construction.
        /// </summary>
        public ScoreValue Benchmark { get; } = RequireStrictlyAscending(Pass, Optimize, Benchmark);

        /// <summary>
        /// Validates that the three bands are strictly ascending.
        /// </summary>
        /// <param name="pass">The lowest band.</param>
        /// <param name="optimize">The middle band.</param>
        /// <param name="benchmark">The highest band.</param>
        /// <returns>The highest band unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the bands are not strictly ascending.</exception>
        private static ScoreValue RequireStrictlyAscending(ScoreValue pass, ScoreValue optimize, ScoreValue benchmark)
        {
            if (pass >= optimize || optimize >= benchmark)
                throw new ArgumentException("Process thresholds must be strictly ascending: Pass < Optimize < Benchmark.");

            return benchmark;
        }
    }
}