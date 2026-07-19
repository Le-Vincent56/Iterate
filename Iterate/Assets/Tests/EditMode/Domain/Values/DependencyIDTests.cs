using System;
using NUnit.Framework;
using Iterate.Domain.Values;

namespace Iterate.Domain.Values.Tests
{
    /// <summary>
    /// Tests identity validation, equality, and string round-tripping of <see cref="DependencyID"/>.
    /// </summary>
    public sealed class DependencyIDTests
    {
        [Test]
        public void Constructor_NullIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new DependencyID(null));
        }

        [Test]
        public void Constructor_EmptyIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new DependencyID(string.Empty));
        }

        [Test]
        public void Equality_SameIdentity_AreEqual()
        {
            Assert.AreEqual(new DependencyID("WB-DEP-001"), new DependencyID("WB-DEP-001"));
        }

        [Test]
        public void Equality_DifferentIdentity_AreNotEqual()
        {
            Assert.AreNotEqual(new DependencyID("WB-DEP-001"), new DependencyID("WB-DEP-005"));
        }

        [Test]
        public void ToString_ReturnsRawIdentity()
        {
            DependencyID id = new("WB-DEP-005");

            Assert.AreEqual("WB-DEP-005", id.ToString());
        }
    }
}
