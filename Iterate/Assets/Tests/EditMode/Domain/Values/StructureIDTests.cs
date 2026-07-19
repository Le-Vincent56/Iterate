using System;
using NUnit.Framework;
using Iterate.Domain.Values;

namespace Iterate.Domain.Values.Tests
{
    /// <summary>
    /// Tests identity validation, equality, and string round-tripping of <see cref="StructureID"/>.
    /// </summary>
    public sealed class StructureIDTests
    {
        [Test]
        public void Constructor_NullIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new StructureID(null));
        }

        [Test]
        public void Constructor_EmptyIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new StructureID(string.Empty));
        }

        [Test]
        public void Equality_SameIdentity_AreEqual()
        {
            Assert.AreEqual(new StructureID("WB-STR-002"), new StructureID("WB-STR-002"));
        }

        [Test]
        public void Equality_DifferentIdentity_AreNotEqual()
        {
            Assert.AreNotEqual(new StructureID("WB-STR-002"), new StructureID("WB-STR-004"));
        }

        [Test]
        public void ToString_ReturnsRawIdentity()
        {
            StructureID id = new("WB-STR-001");

            Assert.AreEqual("WB-STR-001", id.ToString());
        }
    }
}
