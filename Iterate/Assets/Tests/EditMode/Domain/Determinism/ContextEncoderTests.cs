using NUnit.Framework;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Determinism.Tests
{
    /// <summary>
    /// Byte-layout pinning for <see cref="ContextEncoder"/>. Expected byte arrays were computed directly
    /// from the canonical encoding layout (required strings as int32 little-endian length prefix + UTF-8;
    /// each optional slot a presence byte then the same when present; the occurrence ordinal as a fixed
    /// 4-byte little-endian integer). A minimal fixture and a fully-populated fixture (with a non-ASCII
    /// identity) pin the layout; adjacent-slot swaps prove omission cannot collide (CAB-EVT-834).
    /// </summary>
    public sealed class ContextEncoderTests
    {
        [Test]
        public void Encode_MinimalFixture_MatchesExpectedBytes()
        {
            // Arrange — only the two required strings and the ordinal; all seven optional slots absent.
            DecisionContextComponents components = new(
                "seed", null, null, null, null, null, null, null, "purpose", 1);
            byte[] expected =
            {
                0x04, 0x00, 0x00, 0x00, 0x73, 0x65, 0x65, 0x64,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x07, 0x00, 0x00, 0x00, 0x70, 0x75, 0x72, 0x70, 0x6F, 0x73, 0x65,
                0x01, 0x00, 0x00, 0x00
            };

            // Act
            byte[] encoded = ContextEncoder.Encode(components);

            // Assert
            Assert.That(encoded, Is.EqualTo(expected));
        }

        [Test]
        public void Encode_FullyPopulatedFixture_MatchesExpectedBytes()
        {
            // Arrange — every slot present; ProcessIdentity carries the non-ASCII 'ß' (U+00DF -> 0xC3 0x9F).
            DecisionContextComponents components = BuildFullComponents();
            byte[] expected =
            {
                0x07, 0x00, 0x00, 0x00, 0x73, 0x65, 0x65, 0x64, 0x2D, 0x34, 0x32,
                0x01, 0x02, 0x00, 0x00, 0x00, 0x63, 0x31,
                0x01, 0x02, 0x00, 0x00, 0x00, 0x72, 0x31,
                0x01, 0x08, 0x00, 0x00, 0x00, 0x53, 0x79, 0x73, 0x74, 0x65, 0x6D, 0x2D, 0x31,
                0x01, 0x09, 0x00, 0x00, 0x00, 0x50, 0x72, 0x6F, 0x7A, 0x65, 0xC3, 0x9F, 0x2D, 0x37,
                0x01, 0x07, 0x00, 0x00, 0x00, 0x73, 0x63, 0x6F, 0x70, 0x65, 0x2D, 0x39,
                0x01, 0x05, 0x00, 0x00, 0x00, 0x65, 0x76, 0x74, 0x2D, 0x33,
                0x01, 0x08, 0x00, 0x00, 0x00, 0x6F, 0x72, 0x69, 0x67, 0x69, 0x6E, 0x2D, 0x35,
                0x0F, 0x00, 0x00, 0x00, 0x52, 0x61, 0x6E, 0x64, 0x6F, 0x6D, 0x20,
                0x6F, 0x72, 0x64, 0x65, 0x72, 0x69, 0x6E, 0x67,
                0x02, 0x00, 0x00, 0x00
            };

            // Act
            byte[] encoded = ContextEncoder.Encode(components);

            // Assert
            Assert.That(encoded, Is.EqualTo(expected));
        }

        [Test]
        public void Encode_AdjacentOptionalSlotsSwapped_ProduceDifferentBytes()
        {
            // Arrange — value "B" in the SystemIdentity slot vs the RulesetRevision slot: {A, absent, B}
            // and {A, B, absent} across the same adjacent optionals. The presence markers keep them distinct.
            DecisionContextComponents absentThenValue = new(
                "seed", "A", null, "B", null, null, null, null, "purpose", 1);
            DecisionContextComponents valueThenAbsent = new(
                "seed", "A", "B", null, null, null, null, null, "purpose", 1);

            // Act
            byte[] first = ContextEncoder.Encode(absentThenValue);
            byte[] second = ContextEncoder.Encode(valueThenAbsent);

            // Assert
            Assert.That(second, Is.Not.EqualTo(first));
        }

        private static DecisionContextComponents BuildFullComponents()
        {
            return new DecisionContextComponents(
                "seed-42",
                "c1",
                "r1",
                "System-1",
                "Prozeß-7",
                "scope-9",
                "evt-3",
                "origin-5",
                "Random ordering",
                2);
        }
    }
}
