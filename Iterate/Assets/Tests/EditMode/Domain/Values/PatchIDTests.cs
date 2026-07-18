using System;
using NUnit.Framework;
using Iterate.Domain.Values;

namespace Iterate.Domain.Values.Tests
{
    /// <summary>
    /// Tests identity validation, equality, and string round-tripping of <see cref="PatchID"/>.
    /// </summary>
    public sealed class PatchIDTests
    {
        [Test]
        public void Constructor_NullIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new PatchID(null));
        }

        [Test]
        public void Constructor_EmptyIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new PatchID(string.Empty));
        }

        [Test]
        public void Equality_SameIdentity_AreEqual()
        {
            Assert.AreEqual(new PatchID("tutorial-1"), new PatchID("tutorial-1"));
        }

        [Test]
        public void Equality_DifferentIdentity_AreNotEqual()
        {
            Assert.AreNotEqual(new PatchID("tutorial-1"), new PatchID("tutorial-2"));
        }

        [Test]
        public void ToString_ReturnsRawIdentity()
        {
            PatchID id = new("tutorial-1");

            Assert.AreEqual("tutorial-1", id.ToString());
        }
    }
}
