using System;
using NUnit.Framework;
using Iterate.Domain.Values;

namespace Iterate.Domain.Values.Tests
{
    /// <summary>
    /// Tests value validation, equality, and string rendering of <see cref="InstanceID"/> — the typed
    /// instance identity distinct from definition identity.
    /// </summary>
    public sealed class InstanceIDTests
    {
        [Test]
        public void Constructor_Zero_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new InstanceID(0));
        }

        [Test]
        public void Constructor_Negative_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new InstanceID(-1));
        }

        [Test]
        public void Constructor_One_Constructs()
        {
            InstanceID id = new(1);

            Assert.AreEqual(1, id.Value);
        }

        [Test]
        public void Value_RoundTrips()
        {
            InstanceID id = new(42);

            Assert.AreEqual(42, id.Value);
        }

        [Test]
        public void Equality_SameValue_AreEqual()
        {
            Assert.AreEqual(new InstanceID(7), new InstanceID(7));
        }

        [Test]
        public void Equality_DifferentValue_AreNotEqual()
        {
            Assert.AreNotEqual(new InstanceID(7), new InstanceID(8));
        }

        [Test]
        public void ToString_ReturnsHashPrefixedValue()
        {
            InstanceID id = new(42);

            Assert.AreEqual("#42", id.ToString());
        }
    }
}
