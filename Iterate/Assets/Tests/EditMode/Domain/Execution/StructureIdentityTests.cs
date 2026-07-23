using System;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Execution;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the deterministic engine-derived Structure identity strings: the pinned
    /// entry/iteration/evaluation formats over the Structure instance, header position, and ordinals,
    /// and the ordinal validation.
    /// </summary>
    public sealed class StructureIdentityTests
    {
        [Test]
        public void Entry_ComposesInstanceAtPositionHashOrdinal()
        {
            string identity = StructureIdentities.Entry(new InstanceID(5), new SourcePosition(3), 1);

            Assert.AreEqual("#5@3#1", identity);
        }

        [Test]
        public void Entry_LaterOrdinal_IsReserved()
        {
            string identity = StructureIdentities.Entry(new InstanceID(12), new SourcePosition(1), 2);

            Assert.AreEqual("#12@1#2", identity);
        }

        [Test]
        public void Iteration_AppendsIterSuffix()
        {
            string identity = StructureIdentities.Iteration("#5@3#1", 2);

            Assert.AreEqual("#5@3#1/iter-2", identity);
        }

        [Test]
        public void Evaluation_AppendsEvalOne()
        {
            string identity = StructureIdentities.Evaluation("#5@3#1");

            Assert.AreEqual("#5@3#1/eval-1", identity);
        }

        [Test]
        public void Entry_OrdinalZero_Throws()
        {
            Assert.Throws<ArgumentException>(() => StructureIdentities.Entry(new InstanceID(5), new SourcePosition(3), 0));
        }

        [Test]
        public void Iteration_OrdinalZero_Throws()
        {
            Assert.Throws<ArgumentException>(() => StructureIdentities.Iteration("#5@3#1", 0));
        }
    }
}
