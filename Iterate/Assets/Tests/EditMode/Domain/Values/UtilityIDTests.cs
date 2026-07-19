using System;
using NUnit.Framework;
using Iterate.Domain.Values;

namespace Iterate.Domain.Values.Tests
{
    /// <summary>
    /// Tests identity validation, equality, and string round-tripping of <see cref="UtilityID"/>.
    /// </summary>
    public sealed class UtilityIDTests
    {
        [Test]
        public void Constructor_NullIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new UtilityID(null));
        }

        [Test]
        public void Constructor_EmptyIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new UtilityID(string.Empty));
        }

        [Test]
        public void Equality_SameIdentity_AreEqual()
        {
            Assert.AreEqual(new UtilityID("WB-UTL-001"), new UtilityID("WB-UTL-001"));
        }

        [Test]
        public void Equality_DifferentIdentity_AreNotEqual()
        {
            Assert.AreNotEqual(new UtilityID("WB-UTL-001"), new UtilityID("WB-UTL-002"));
        }

        [Test]
        public void ToString_ReturnsRawIdentity()
        {
            UtilityID id = new("WB-UTL-003");

            Assert.AreEqual("WB-UTL-003", id.ToString());
        }
    }
}
