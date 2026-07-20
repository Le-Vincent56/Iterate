using System;
using NUnit.Framework;
using Iterate.Domain.Compilation;

namespace Iterate.Domain.Compilation.Tests
{
    /// <summary>
    /// Tests line-number validation, round-tripping, and equality of <see cref="SourcePosition"/> — the
    /// fixed, one-based numbered position in a source arrangement.
    /// </summary>
    public sealed class SourcePositionTests
    {
        [Test]
        public void Constructor_Zero_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new SourcePosition(0));
        }

        [Test]
        public void Constructor_Negative_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new SourcePosition(-1));
        }

        [Test]
        public void Constructor_One_Constructs()
        {
            SourcePosition position = new(1);

            Assert.AreEqual(1, position.LineNumber);
        }

        [Test]
        public void LineNumber_RoundTrips()
        {
            SourcePosition position = new(9);

            Assert.AreEqual(9, position.LineNumber);
        }

        [Test]
        public void Equality_SameLineNumber_AreEqual()
        {
            Assert.AreEqual(new SourcePosition(3), new SourcePosition(3));
        }

        [Test]
        public void Equality_DifferentLineNumber_AreNotEqual()
        {
            Assert.AreNotEqual(new SourcePosition(3), new SourcePosition(4));
        }
    }
}
