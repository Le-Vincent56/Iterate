using System;
using NUnit.Framework;
using Iterate.Domain.Values;

namespace Iterate.Domain.Values.Tests
{
    /// <summary>
    /// Tests identity validation, equality, and string round-tripping of <see cref="DirectiveID"/>.
    /// </summary>
    public sealed class DirectiveIDTests
    {
        [Test]
        public void Constructor_NullIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new DirectiveID(null));
        }

        [Test]
        public void Constructor_EmptyIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new DirectiveID(string.Empty));
        }

        [Test]
        public void Equality_SameIdentity_AreEqual()
        {
            Assert.AreEqual(new DirectiveID("WB-DIR-003"), new DirectiveID("WB-DIR-003"));
        }

        [Test]
        public void Equality_DifferentIdentity_AreNotEqual()
        {
            Assert.AreNotEqual(new DirectiveID("WB-DIR-003"), new DirectiveID("WB-DIR-004"));
        }

        [Test]
        public void ToString_ReturnsRawIdentity()
        {
            DirectiveID id = new("WB-DIR-004");

            Assert.AreEqual("WB-DIR-004", id.ToString());
        }
    }
}
