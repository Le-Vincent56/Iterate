using System;

namespace Iterate.Domain.Determinism
{
    /// <summary>
    /// Owned implementation of the public-domain xxHash64 algorithm, hashing an encoded byte span to a
    /// 64-bit digest. Fixed under revision identity <c>iterate-rng-1</c>: the prime constants, four-lane
    /// stripe layout, tail processing, and avalanche finalisation are part of the reproducibility
    /// contract and never change without a new revision identity. Every multi-byte read is little-endian
    /// regardless of host endianness, so a digest is stable across platforms.
    /// </summary>
    public static class XxHash64
    {
        /// <summary>
        /// The first 64-bit prime (PRIME64_1).
        /// </summary>
        private const ulong Prime1 = 0x9E3779B185EBCA87UL;

        /// <summary>
        /// The second 64-bit prime (PRIME64_2).
        /// </summary>
        private const ulong Prime2 = 0xC2B2AE3D27D4EB4FUL;

        /// <summary>
        /// The third 64-bit prime (PRIME64_3).
        /// </summary>
        private const ulong Prime3 = 0x165667B19E3779F9UL;

        /// <summary>
        /// The fourth 64-bit prime (PRIME64_4).
        /// </summary>
        private const ulong Prime4 = 0x85EBCA77C2B2AE63UL;

        /// <summary>
        /// The fifth 64-bit prime (PRIME64_5).
        /// </summary>
        private const ulong Prime5 = 0x27D4EB2F165667C5UL;

        /// <summary>
        /// Computes the xxHash64 digest of <paramref name="data"/> under <paramref name="seed"/>.
        /// </summary>
        /// <param name="data">The bytes to hash; may be empty.</param>
        /// <param name="seed">The 64-bit seed.</param>
        /// <returns>The 64-bit xxHash64 digest.</returns>
        public static ulong Compute(ReadOnlySpan<byte> data, ulong seed)
        {
            unchecked
            {
                int length = data.Length;
                int index = 0;
                ulong accumulator;

                if (length >= 32)
                {
                    ulong lane1 = seed + Prime1 + Prime2;
                    ulong lane2 = seed + Prime2;
                    ulong lane3 = seed;
                    ulong lane4 = seed - Prime1;

                    int stripeLimit = length - 32;
                    while (index <= stripeLimit)
                    {
                        lane1 = Round(lane1, ReadUInt64LittleEndian(data, index));
                        lane2 = Round(lane2, ReadUInt64LittleEndian(data, index + 8));
                        lane3 = Round(lane3, ReadUInt64LittleEndian(data, index + 16));
                        lane4 = Round(lane4, ReadUInt64LittleEndian(data, index + 24));
                        index += 32;
                    }

                    accumulator = RotateLeft(lane1, 1) + RotateLeft(lane2, 7)
                        + RotateLeft(lane3, 12) + RotateLeft(lane4, 18);
                    accumulator = MergeRound(accumulator, lane1);
                    accumulator = MergeRound(accumulator, lane2);
                    accumulator = MergeRound(accumulator, lane3);
                    accumulator = MergeRound(accumulator, lane4);
                }
                else
                {
                    accumulator = seed + Prime5;
                }

                accumulator += (ulong)length;

                while (index + 8 <= length)
                {
                    ulong lane = Round(0UL, ReadUInt64LittleEndian(data, index));
                    accumulator ^= lane;
                    accumulator = RotateLeft(accumulator, 27) * Prime1 + Prime4;
                    index += 8;
                }

                if (index + 4 <= length)
                {
                    accumulator ^= ReadUInt32LittleEndian(data, index) * Prime1;
                    accumulator = RotateLeft(accumulator, 23) * Prime2 + Prime3;
                    index += 4;
                }

                while (index < length)
                {
                    accumulator ^= data[index] * Prime5;
                    accumulator = RotateLeft(accumulator, 11) * Prime1;
                    index += 1;
                }

                return Avalanche(accumulator);
            }
        }

        /// <summary>
        /// Applies one xxHash64 round, mixing <paramref name="input"/> into <paramref name="accumulator"/>.
        /// </summary>
        /// <param name="accumulator">The lane accumulator.</param>
        /// <param name="input">The little-endian 64-bit input word.</param>
        /// <returns>The updated accumulator.</returns>
        private static ulong Round(ulong accumulator, ulong input)
        {
            unchecked
            {
                accumulator += input * Prime2;
                accumulator = RotateLeft(accumulator, 31);
                accumulator *= Prime1;
                return accumulator;
            }
        }

        /// <summary>
        /// Merges a finished stripe lane into the combined accumulator (the ≥32-byte path).
        /// </summary>
        /// <param name="accumulator">The combined accumulator.</param>
        /// <param name="lane">The finished lane value.</param>
        /// <returns>The updated accumulator.</returns>
        private static ulong MergeRound(ulong accumulator, ulong lane)
        {
            unchecked
            {
                ulong rounded = Round(0UL, lane);
                accumulator ^= rounded;
                accumulator = accumulator * Prime1 + Prime4;
                return accumulator;
            }
        }

        /// <summary>
        /// Applies the final avalanche, spreading every input bit across the digest.
        /// </summary>
        /// <param name="accumulator">The accumulator after tail processing.</param>
        /// <returns>The finalised 64-bit digest.</returns>
        private static ulong Avalanche(ulong accumulator)
        {
            unchecked
            {
                accumulator ^= accumulator >> 33;
                accumulator *= Prime2;
                accumulator ^= accumulator >> 29;
                accumulator *= Prime3;
                accumulator ^= accumulator >> 32;
                return accumulator;
            }
        }

        /// <summary>
        /// Rotates <paramref name="value"/> left by <paramref name="bits"/> positions.
        /// </summary>
        /// <param name="value">The value to rotate.</param>
        /// <param name="bits">The rotation amount, from 1 to 63.</param>
        /// <returns>The left-rotated value.</returns>
        private static ulong RotateLeft(ulong value, int bits) => (value << bits) | (value >> (64 - bits));

        /// <summary>
        /// Reads eight bytes at <paramref name="index"/> as a little-endian 64-bit word.
        /// </summary>
        /// <param name="data">The source span.</param>
        /// <param name="index">The offset of the first byte.</param>
        /// <returns>The assembled little-endian 64-bit value.</returns>
        private static ulong ReadUInt64LittleEndian(ReadOnlySpan<byte> data, int index)
        {
            return (ulong)data[index]
                | ((ulong)data[index + 1] << 8)
                | ((ulong)data[index + 2] << 16)
                | ((ulong)data[index + 3] << 24)
                | ((ulong)data[index + 4] << 32)
                | ((ulong)data[index + 5] << 40)
                | ((ulong)data[index + 6] << 48)
                | ((ulong)data[index + 7] << 56);
        }

        /// <summary>
        /// Reads four bytes at <paramref name="index"/> as a little-endian 32-bit word.
        /// </summary>
        /// <param name="data">The source span.</param>
        /// <param name="index">The offset of the first byte.</param>
        /// <returns>The assembled little-endian 32-bit value.</returns>
        private static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> data, int index)
        {
            return (uint)data[index]
                | ((uint)data[index + 1] << 8)
                | ((uint)data[index + 2] << 16)
                | ((uint)data[index + 3] << 24);
        }
    }
}