using System;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;

namespace Iterate.Domain.Compilation.Tests
{
    /// <summary>
    /// Tests that <see cref="CoreLine"/> requires a non-empty identity and a non-null typed operation,
    /// round-trips them, and compares by value over the typed operation (UX-SRC-016 arrangement
    /// equivalence holds by construction).
    /// </summary>
    public sealed class CoreLineTests
    {
        [Test]
        public void Constructor_NullIdentity_Throws()
        {
            CoreLineOperation operation = new(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1));

            Assert.Throws<ArgumentException>(() => _ = new CoreLine(null, operation));
        }

        [Test]
        public void Constructor_EmptyIdentity_Throws()
        {
            CoreLineOperation operation = new(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1));

            Assert.Throws<ArgumentException>(() => _ = new CoreLine(string.Empty, operation));
        }

        [Test]
        public void Constructor_NullOperation_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreLine("core-01", null));
        }

        [Test]
        public void Constructor_BothPresent_RoundTrips()
        {
            CoreLineOperation operation = new(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1));

            CoreLine line = new("core-01", operation);

            Assert.AreEqual("core-01", line.Identity);
            Assert.AreEqual(operation, line.Operation);
        }

        [Test]
        public void Equality_SameValues_AreEqual()
        {
            CoreLine left = new("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
            CoreLine right = new("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));

            Assert.AreEqual(left, right);
        }

        [Test]
        public void Equality_DifferentOperation_AreNotEqual()
        {
            CoreLine left = new("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1)));
            CoreLine right = new("core-01", new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(2)));

            Assert.AreNotEqual(left, right);
        }
    }
}
