using System;
using NUnit.Framework;
using Iterate.Domain.Compilation;

namespace Iterate.Domain.Compilation.Tests
{
    /// <summary>
    /// Tests that <see cref="CoreLine"/> requires both a non-empty identity and a non-empty operation
    /// payload, round-trips them, and compares by value. The payload is an opaque immutable string this
    /// feature does not execute.
    /// </summary>
    public sealed class CoreLineTests
    {
        [Test]
        public void Constructor_NullIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreLine(null, "Value = 1"));
        }

        [Test]
        public void Constructor_EmptyIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreLine(string.Empty, "Value = 1"));
        }

        [Test]
        public void Constructor_NullOperationText_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreLine("core-01", null));
        }

        [Test]
        public void Constructor_EmptyOperationText_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreLine("core-01", string.Empty));
        }

        [Test]
        public void Constructor_BothNonEmpty_RoundTrips()
        {
            CoreLine line = new("core-01", "Value = 1");

            Assert.AreEqual("core-01", line.Identity);
            Assert.AreEqual("Value = 1", line.OperationText);
        }

        [Test]
        public void Equality_SameValues_AreEqual()
        {
            Assert.AreEqual(new CoreLine("core-01", "Value = 1"), new CoreLine("core-01", "Value = 1"));
        }

        [Test]
        public void Equality_DifferentOperationText_AreNotEqual()
        {
            Assert.AreNotEqual(new CoreLine("core-01", "Value = 1"), new CoreLine("core-01", "Value = 2"));
        }
    }
}
