using System;
using NUnit.Framework;
using Iterate.Domain.Determinism;

namespace Iterate.Domain.Determinism.Tests
{
    /// <summary>
    /// Tests the canonical CAB-EVT-799 comparator <see cref="CandidateOrderingKey.Compare"/> — definition
    /// then instance then source then additional tie-break, ordinal string comparison, null sorting before
    /// any value, and a zero result only for fully identical keys — plus the key's construction validation
    /// (definition required non-empty; optional components null-for-absent, never empty).
    /// </summary>
    public sealed class CandidateOrderingTests
    {
        [Test]
        public void Compare_DefinitionIdentityDominates()
        {
            // Arrange — a later definition with an earlier instance still sorts after (definition wins).
            CandidateOrderingKey earlier = new("A", "z", null, null);
            CandidateOrderingKey later = new("B", "a", null, null);

            // Act & Assert
            Assert.That(CandidateOrderingKey.Compare(earlier, later), Is.LessThan(0));
            Assert.That(CandidateOrderingKey.Compare(later, earlier), Is.GreaterThan(0));
        }

        [Test]
        public void Compare_EqualDefinition_OrdersByInstance()
        {
            CandidateOrderingKey first = new("A", "a", null, null);
            CandidateOrderingKey second = new("A", "b", null, null);

            Assert.That(CandidateOrderingKey.Compare(first, second), Is.LessThan(0));
        }

        [Test]
        public void Compare_EqualDefinitionAndInstance_OrdersBySource()
        {
            CandidateOrderingKey first = new("A", "a", "s1", null);
            CandidateOrderingKey second = new("A", "a", "s2", null);

            Assert.That(CandidateOrderingKey.Compare(first, second), Is.LessThan(0));
        }

        [Test]
        public void Compare_DistinctInAdditionalTieBreakOnly_IsNonZero()
        {
            CandidateOrderingKey first = new("A", "a", "s", "1");
            CandidateOrderingKey second = new("A", "a", "s", "2");

            Assert.That(CandidateOrderingKey.Compare(first, second), Is.LessThan(0));
            Assert.That(CandidateOrderingKey.Compare(second, first), Is.GreaterThan(0));
        }

        [Test]
        public void Compare_NullComponentSortsBeforeValue()
        {
            // Definition equal; the absent (null) instance sorts before any present instance.
            CandidateOrderingKey absent = new("A", null, null, null);
            CandidateOrderingKey present = new("A", "x", null, null);

            Assert.That(CandidateOrderingKey.Compare(absent, present), Is.LessThan(0));
            Assert.That(CandidateOrderingKey.Compare(present, absent), Is.GreaterThan(0));
        }

        [Test]
        public void Compare_FullyIdenticalKeys_ReturnsZero()
        {
            CandidateOrderingKey first = new("A", "a", "s", "t");
            CandidateOrderingKey second = new("A", "a", "s", "t");

            Assert.That(CandidateOrderingKey.Compare(first, second), Is.EqualTo(0));
        }

        [Test]
        public void Compare_BothNullOptionalComponents_AreEqualThere()
        {
            CandidateOrderingKey first = new("A", null, null, null);
            CandidateOrderingKey second = new("A", null, null, null);

            Assert.That(CandidateOrderingKey.Compare(first, second), Is.EqualTo(0));
        }

        [Test]
        public void Constructor_EmptyDefinitionIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CandidateOrderingKey("", null, null, null));
        }

        [Test]
        public void Constructor_NullDefinitionIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CandidateOrderingKey(null, null, null, null));
        }

        [Test]
        public void Constructor_EmptyOptionalComponent_Throws(
            [Values(1, 2, 3)] int slot)
        {
            Assert.Throws<ArgumentException>(() => new CandidateOrderingKey(
                "A",
                slot == 1 ? "" : null,
                slot == 2 ? "" : null,
                slot == 3 ? "" : null));
        }

        [Test]
        public void Constructor_NullOptionalComponents_Allowed()
        {
            Assert.DoesNotThrow(() => new CandidateOrderingKey("A", null, null, null));
        }
    }
}
