using System;
using NUnit.Framework;
using Iterate.Domain.Values;

namespace Iterate.Domain.Values.Tests
{
    /// <summary>
    /// Tests identity validation, equality, and string round-tripping of <see cref="ProcessID"/>.
    /// </summary>
    public sealed class ProcessIDTests
    {
        [Test]
        public void Constructor_NullIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ProcessID(null));
        }

        [Test]
        public void Constructor_EmptyIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new ProcessID(string.Empty));
        }

        [Test]
        public void Equality_SameIdentity_AreEqual()
        {
            Assert.AreEqual(new ProcessID("tutorial-1"), new ProcessID("tutorial-1"));
        }

        [Test]
        public void Equality_DifferentIdentity_AreNotEqual()
        {
            Assert.AreNotEqual(new ProcessID("tutorial-1"), new ProcessID("tutorial-2"));
        }

        [Test]
        public void ToString_ReturnsRawIdentity()
        {
            ProcessID id = new("tutorial-1");

            Assert.AreEqual("tutorial-1", id.ToString());
        }
    }
}
