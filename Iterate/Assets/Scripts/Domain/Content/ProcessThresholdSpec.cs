using System;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A Process's three authored success thresholds. Strictly ascending, so the submitted tier is
    /// unambiguous. Authored explicitly rather than derived: the tutorial Processes are ratio
    /// exceptions.
    /// </summary>
    /// <param name="Pass">The Pass threshold.</param>
    /// <param name="Optimize">The Optimize threshold.</param>
    /// <param name="Benchmark">The Benchmark threshold.</param>
    public sealed record ProcessThresholdSpec(int Pass, int Optimize, int Benchmark)
    {
        /// <summary>
        /// The Pass threshold. Validated as the lowest of a strictly ascending triple at construction.
        /// </summary>
        public int Pass { get; } = RequireAscending(Pass, Optimize, Benchmark);

        /// <summary>
        /// Validates that the three thresholds strictly ascend and returns the Pass threshold unchanged.
        /// </summary>
        /// <param name="pass">The candidate Pass threshold.</param>
        /// <param name="optimize">The candidate Optimize threshold.</param>
        /// <param name="benchmark">The candidate Benchmark threshold.</param>
        /// <returns>The Pass threshold unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the thresholds do not strictly ascend.</exception>
        private static int RequireAscending(int pass, int optimize, int benchmark)
        {
            if (optimize <= pass)
                throw new ArgumentException("A Process Optimize threshold must exceed its Pass threshold.", nameof(optimize));

            if (benchmark <= optimize)
                throw new ArgumentException("A Process Benchmark threshold must exceed its Optimize threshold.", nameof(benchmark));

            return pass;
        }
    }
}