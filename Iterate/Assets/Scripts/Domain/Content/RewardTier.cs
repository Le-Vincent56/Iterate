namespace Iterate.Domain.Content
{
    /// <summary>
    /// The three success tiers a reward component is awarded at. Serialized in JSON as PASS,
    /// OPTIMIZE, BENCHMARK.
    /// </summary>
    public enum RewardTier
    {
        Pass,
        Optimize,
        Benchmark
    }
}