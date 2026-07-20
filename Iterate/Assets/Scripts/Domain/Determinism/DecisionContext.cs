namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// An isolated random-decision context: its <see cref="DecisionContextComponents"/> plus the 64-bit
    /// context hash derived from their canonical encoding. Two contexts derived from equal components are
    /// value-equal and carry the same hash (stateless reproduction). The components are retained so
    /// divergence evidence can identify which component differed — the hash alone cannot.
    /// </summary>
    public sealed record DecisionContext
    {
        /// <summary>
        /// The components this context was derived from, retained for divergence evidence.
        /// </summary>
        public DecisionContextComponents Components { get; }

        /// <summary>
        /// The 64-bit context hash, the xxHash64 of the canonical encoding under seed 0.
        /// </summary>
        public ulong Hash { get; }

        private DecisionContext(DecisionContextComponents components, ulong hash)
        {
            Components = components;
            Hash = hash;
        }

        /// <summary>
        /// Derives a context from <paramref name="components"/>: hashes their canonical encoding with
        /// xxHash64 (seed 0) and retains the components.
        /// </summary>
        /// <param name="components">The decision-context components.</param>
        /// <returns>The derived context carrying the components and their hash.</returns>
        public static DecisionContext Derive(DecisionContextComponents components)
        {
            byte[] encoded = ContextEncoder.Encode(components);
            ulong hash = XxHash64.Compute(encoded, 0UL);
            return new DecisionContext(components, hash);
        }
    }
}