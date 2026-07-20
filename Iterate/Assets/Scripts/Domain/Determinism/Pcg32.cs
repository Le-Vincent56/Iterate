using System;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// Owned PCG-XSH-RR 32 generator (the <c>pcg32</c> setseq variant). Seeded from a 64-bit state word
    /// and a 64-bit sequence selector following the reference <c>pcg32_srandom_r</c> scheme; each
    /// <see cref="NextUInt32()"/> advances the LCG state and applies the XSH-RR output permutation. Fixed
    /// under revision identity <c>iterate-rng-1</c>: the multiplier, seeding, output permutation, and
    /// bounded rejection scheme are part of the reproducibility contract. One stream is created per
    /// decision inside the service and never escapes, so unrelated decisions cannot perturb each other.
    /// </summary>
    public sealed class Pcg32
    {
        /// <summary>
        /// The reference PCG 64-bit LCG multiplier.
        /// </summary>
        private const ulong Multiplier = 6364136223846793005UL;

        /// <summary>
        /// The stream increment, fixed at construction as (sequence &lt;&lt; 1) | 1.
        /// </summary>
        private readonly ulong _increment;

        /// <summary>
        /// The evolving 64-bit LCG state.
        /// </summary>
        private ulong _state;

        /// <summary>
        /// Seeds the generator following the reference <c>pcg32_srandom_r</c> scheme.
        /// </summary>
        /// <param name="initialState">The starting state seed (the context hash in service use).</param>
        /// <param name="initialSequence">The sequence selector; distinct selectors yield distinct streams.</param>
        public Pcg32(ulong initialState, ulong initialSequence)
        {
            unchecked
            {
                _increment = (initialSequence << 1) | 1UL;
                _state = 0UL;
                Step();
                _state += initialState;
                Step();
            }
        }

        /// <summary>
        /// Draws the next 32-bit value and advances the stream.
        /// </summary>
        /// <returns>The next pseudo-random 32-bit value.</returns>
        public uint NextUInt32()
        {
            unchecked
            {
                ulong previous = _state;
                Step();
                uint xorShifted = (uint)(((previous >> 18) ^ previous) >> 27);
                int rotation = (int)(previous >> 59);
                return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
            }
        }

        /// <summary>
        /// Draws the next 32-bit value in the range [0, <paramref name="exclusiveUpperBound"/>) without
        /// modulo bias, using the reference rejection-sampling scheme.
        /// </summary>
        /// <param name="exclusiveUpperBound">The exclusive upper bound; must be one or greater.</param>
        /// <returns>An unbiased value in [0, <paramref name="exclusiveUpperBound"/>).</returns>
        public uint NextUInt32(uint exclusiveUpperBound)
        {
            if (exclusiveUpperBound == 0U)
            {
                throw new ArgumentException(
                    "An exclusive upper bound of at least one is required.",
                    nameof(exclusiveUpperBound)
                );
            }

            unchecked
            {
                uint threshold = (uint)((0x100000000UL - exclusiveUpperBound) % exclusiveUpperBound);
                while (true)
                {
                    uint candidate = NextUInt32();
                    if (candidate >= threshold)
                        return candidate % exclusiveUpperBound;
                }
            }
        }

        /// <summary>
        /// Advances the LCG state by one step: state = state * multiplier + increment.
        /// </summary>
        private void Step()
        {
            unchecked
            {
                _state = _state * Multiplier + _increment;
            }
        }
    }
}