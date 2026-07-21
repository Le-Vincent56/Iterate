using System;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;

namespace Iterate.Domain.Compilation.Tests
{
    /// <summary>
    /// Tests the typed <see cref="CoreLineOperation"/> payload: each of the closed
    /// <see cref="CoreLineOperator"/> forms constructs and round-trips its operator, target register, and
    /// operand; a null operand is rejected; and two independently built operations compare by value.
    /// </summary>
    public sealed class CoreLineOperationTests
    {
        [Test]
        public void Constructor_Assign_RoundTrips()
        {
            OperandSpec operand = OperandSpec.FromConstant(1);

            CoreLineOperation operation = new(CoreLineOperator.Assign, CoreRegister.Value, operand);

            Assert.AreEqual(CoreLineOperator.Assign, operation.Operator);
            Assert.AreEqual(CoreRegister.Value, operation.Target);
            Assert.AreEqual(operand, operation.Operand);
        }

        [Test]
        public void Constructor_Add_RoundTrips()
        {
            OperandSpec operand = OperandSpec.FromConstant(2);

            CoreLineOperation operation = new(CoreLineOperator.Add, CoreRegister.Score, operand);

            Assert.AreEqual(CoreLineOperator.Add, operation.Operator);
            Assert.AreEqual(CoreRegister.Score, operation.Target);
            Assert.AreEqual(operand, operation.Operand);
        }

        [Test]
        public void Constructor_Multiply_RoundTrips()
        {
            OperandSpec operand = OperandSpec.FromConstant(3);

            CoreLineOperation operation = new(CoreLineOperator.Multiply, CoreRegister.Signal, operand);

            Assert.AreEqual(CoreLineOperator.Multiply, operation.Operator);
            Assert.AreEqual(CoreRegister.Signal, operation.Target);
            Assert.AreEqual(operand, operation.Operand);
        }

        [Test]
        public void Constructor_NullOperand_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = new CoreLineOperation(CoreLineOperator.Assign, CoreRegister.Value, null));
        }

        [Test]
        public void Equality_SameValues_AreEqual()
        {
            CoreLineOperation left = new(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value));
            CoreLineOperation right = new(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value));

            Assert.AreEqual(left, right);
        }

        [Test]
        public void Equality_DifferentOperator_AreNotEqual()
        {
            CoreLineOperation left = new(CoreLineOperator.Add, CoreRegister.Value, OperandSpec.FromConstant(1));
            CoreLineOperation right = new(CoreLineOperator.Multiply, CoreRegister.Value, OperandSpec.FromConstant(1));

            Assert.AreNotEqual(left, right);
        }

        [Test]
        public void Equality_DifferentTarget_AreNotEqual()
        {
            CoreLineOperation left = new(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1));
            CoreLineOperation right = new(CoreLineOperator.Assign, CoreRegister.Signal, OperandSpec.FromConstant(1));

            Assert.AreNotEqual(left, right);
        }

        [Test]
        public void Equality_DifferentOperand_AreNotEqual()
        {
            CoreLineOperation left = new(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1));
            CoreLineOperation right = new(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(2));

            Assert.AreNotEqual(left, right);
        }
    }
}
