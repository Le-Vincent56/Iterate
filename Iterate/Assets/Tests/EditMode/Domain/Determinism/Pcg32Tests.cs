using System;
using NUnit.Framework;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Determinism.Tests
{
    /// <summary>
    /// Known-answer and behavioural tests for <see cref="Pcg32"/> (PCG-XSH-RR 32). The seeded output
    /// stream and the bounded-draw mapping were re-derived from the PCG reference algorithm
    /// (imneme pcg-c, seeding <c>pcg32_srandom_r(&amp;rng, 42u, 54u)</c>) on July 20, 2026 and pinned
    /// exactly, so a mismatch indicts the implementation, not the vector.
    /// </summary>
    public sealed class Pcg32Tests
    {
        [Test]
        public void NextUInt32_ReferenceSeed_ProducesReferenceStream()
        {
            // Arrange — the reference demo's deterministic seed (initstate 42, initseq 54).
            Pcg32 generator = new(42UL, 54UL);
            uint[] expected =
            {
                0xA15C02B7u, 0x7B47F409u, 0xBA1D3330u, 0x83D2F293u, 0xBFA4784Bu, 0xCBED606Eu
            };

            // Act & Assert
            for (int index = 0; index < expected.Length; index++)
                Assert.That(generator.NextUInt32(), Is.EqualTo(expected[index]), $"draw {index}");
        }

        [Test]
        public void NextUInt32Bounded_ReferenceSeed_MatchesReferenceMapping()
        {
            // Arrange — reference pcg32_boundedrand_r(rng, 10) over the seeded stream.
            Pcg32 generator = new(42UL, 54UL);
            uint[] expected = { 3u, 7u, 4u, 5u, 5u, 6u, 5u, 5u };

            // Act & Assert
            for (int index = 0; index < expected.Length; index++)
                Assert.That(generator.NextUInt32(10u), Is.EqualTo(expected[index]), $"draw {index}");
        }

        [Test]
        public void NextUInt32Bounded_BoundOfOne_IsAlwaysZero()
        {
            Pcg32 generator = new(42UL, 54UL);

            for (int index = 0; index < 16; index++)
                Assert.That(generator.NextUInt32(1u), Is.EqualTo(0u), $"draw {index}");
        }

        [Test]
        public void NextUInt32Bounded_BoundOfZero_Throws()
        {
            Pcg32 generator = new(42UL, 54UL);

            Assert.Throws<ArgumentException>(() => generator.NextUInt32(0u));
        }

        [Test]
        public void NextUInt32_EqualSeeds_ProduceEqualSequences()
        {
            // Arrange
            Pcg32 first = new(42UL, 54UL);
            Pcg32 second = new(42UL, 54UL);

            // Act & Assert
            for (int index = 0; index < 8; index++)
                Assert.That(second.NextUInt32(), Is.EqualTo(first.NextUInt32()), $"draw {index}");
        }

        [Test]
        public void NextUInt32_DifferingSequenceSelector_ProducesDifferentSequences()
        {
            // Arrange — same state seed, different sequence selector (54 vs 55).
            Pcg32 sequence54 = new(42UL, 54UL);
            Pcg32 sequence55 = new(42UL, 55UL);

            // Act
            uint fromFiftyFour = sequence54.NextUInt32();
            uint fromFiftyFive = sequence55.NextUInt32();

            // Assert
            Assert.That(fromFiftyFour, Is.EqualTo(0xA15C02B7u));
            Assert.That(fromFiftyFive, Is.EqualTo(0xADD2C78Fu));
            Assert.That(fromFiftyFive, Is.Not.EqualTo(fromFiftyFour));
        }
    }
}
