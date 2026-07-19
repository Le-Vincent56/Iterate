using System;
using NUnit.Framework;
using Iterate.Domain.Values;

namespace Iterate.Domain.Values.Tests
{
    /// <summary>
    /// Tests identity validation, equality, and string round-tripping of <see cref="InstructionID"/>.
    /// </summary>
    public sealed class InstructionIDTests
    {
        [Test]
        public void Constructor_NullIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new InstructionID(null));
        }

        [Test]
        public void Constructor_EmptyIdentity_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new InstructionID(string.Empty));
        }

        [Test]
        public void Equality_SameIdentity_AreEqual()
        {
            Assert.AreEqual(new InstructionID("WB-INS-002"), new InstructionID("WB-INS-002"));
        }

        [Test]
        public void Equality_DifferentIdentity_AreNotEqual()
        {
            Assert.AreNotEqual(new InstructionID("WB-INS-002"), new InstructionID("WB-INS-003"));
        }

        [Test]
        public void ToString_ReturnsRawIdentity()
        {
            InstructionID id = new("WB-INS-014");

            Assert.AreEqual("WB-INS-014", id.ToString());
        }
    }
}
