using System;
using System.Text;
using NUnit.Framework;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Determinism.Tests
{
    /// <summary>
    /// Known-answer tests for <see cref="XxHash64"/>. Every pinned digest was verified against the
    /// official xxHash reference library (libxxhash via the xxhash binding) on July 20, 2026 — never
    /// transcribed from memory — so a mismatch indicts the implementation, not the vector. Coverage
    /// spans empty input, one- and three-byte inputs (short tail path), a 64-byte input (the
    /// four-lane stripe loop), and nonzero seeds.
    /// </summary>
    public sealed class XxHash64Tests
    {
        [Test]
        public void Compute_EmptyInputSeedZero_MatchesReference()
        {
            // Arrange
            ReadOnlySpan<byte> data = Array.Empty<byte>();

            // Act
            ulong result = XxHash64.Compute(data, 0UL);

            // Assert
            Assert.That(result, Is.EqualTo(0xEF46DB3751D8E999UL));
        }

        [Test]
        public void Compute_SingleByteSeedZero_MatchesReference()
        {
            ReadOnlySpan<byte> data = Encoding.UTF8.GetBytes("a");

            ulong result = XxHash64.Compute(data, 0UL);

            Assert.That(result, Is.EqualTo(0xD24EC4F1A98C6E5BUL));
        }

        [Test]
        public void Compute_ThreeBytesSeedZero_MatchesReference()
        {
            ReadOnlySpan<byte> data = Encoding.UTF8.GetBytes("abc");

            ulong result = XxHash64.Compute(data, 0UL);

            Assert.That(result, Is.EqualTo(0x44BC2CF5AD770999UL));
        }

        [Test]
        public void Compute_SixtyFourByteInputSeedZero_MatchesReference()
        {
            // Arrange — 64 bytes 0x00..0x3F exercise the >= 32-byte four-lane stripe loop.
            ReadOnlySpan<byte> data = SequentialBytes(64);

            // Act
            ulong result = XxHash64.Compute(data, 0UL);

            // Assert
            Assert.That(result, Is.EqualTo(0xF7C67301DB6713F0UL));
        }

        [Test]
        public void Compute_SixtyFourByteInputNonzeroSeed_MatchesReference()
        {
            ReadOnlySpan<byte> data = SequentialBytes(64);

            ulong result = XxHash64.Compute(data, 42UL);

            Assert.That(result, Is.EqualTo(0xCE47892C1E53BE8EUL));
        }

        [Test]
        public void Compute_EmptyInputNonzeroSeed_MatchesReference()
        {
            ReadOnlySpan<byte> data = Array.Empty<byte>();

            ulong result = XxHash64.Compute(data, 1UL);

            Assert.That(result, Is.EqualTo(0xD5AFBA1336A3BE4BUL));
        }

        [Test]
        public void Compute_SameInputTwice_IsDeterministic()
        {
            ReadOnlySpan<byte> data = Encoding.UTF8.GetBytes("abc");

            ulong first = XxHash64.Compute(data, 0UL);
            ulong second = XxHash64.Compute(Encoding.UTF8.GetBytes("abc"), 0UL);

            Assert.That(second, Is.EqualTo(first));
        }

        private static byte[] SequentialBytes(int count)
        {
            byte[] bytes = new byte[count];
            for (int index = 0; index < count; index++)
                bytes[index] = (byte)index;

            return bytes;
        }
    }
}
