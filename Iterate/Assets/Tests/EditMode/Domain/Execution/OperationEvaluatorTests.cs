using System;
using NUnit.Framework;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Execution;
using Iterate.Domain.Trace;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution.Tests
{
    /// <summary>
    /// Tests the pure <see cref="OperationEvaluator"/>: it classifies Assign versus increase, resolves
    /// constant/register/line-number operands, computes prior/final/requested/delta, treats a zero-delta
    /// assignment as a distinct resolved fact, and never writes the registers it reads.
    /// </summary>
    public sealed class OperationEvaluatorTests
    {
        private static ExecutionRegisters Seeded()
        {
            return new ExecutionRegisters(new InitialExecutionState(new ValueAmount(5), new SignalValue(2), new ScoreValue(7)));
        }

        [Test]
        public void EvaluateCoreLine_Assign_ClassifiesAndComputes()
        {
            CoreLineOperation operation = new(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(1));

            EvaluatedOperation result = OperationEvaluator.EvaluateCoreLine(operation, Seeded(), new SourcePosition(1));

            Assert.AreEqual(CoreRegister.Value, result.Register);
            Assert.AreEqual(QuantityOperationType.Assign, result.OperationType);
            Assert.AreEqual(ExecutionEventSubtypes.QuantityAssigned, result.Subtype);
            Assert.AreEqual(1, result.RequestedAmount);
            Assert.AreEqual(5, result.PriorValue);
            Assert.AreEqual(1, result.FinalValue);
            Assert.AreEqual(-4, result.FinalDelta);
        }

        [Test]
        public void EvaluateCoreLine_AssignToCurrentValue_IsZeroDeltaButResolved()
        {
            CoreLineOperation operation = new(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(5));

            EvaluatedOperation result = OperationEvaluator.EvaluateCoreLine(operation, Seeded(), new SourcePosition(1));

            Assert.AreEqual(QuantityOperationType.Assign, result.OperationType);
            Assert.AreEqual(ExecutionEventSubtypes.QuantityAssigned, result.Subtype);
            Assert.AreEqual(5, result.PriorValue);
            Assert.AreEqual(5, result.FinalValue);
            Assert.AreEqual(0, result.FinalDelta);
        }

        [Test]
        public void EvaluateCoreLine_AddRegister_ClassifiesIncrease()
        {
            CoreLineOperation operation = new(CoreLineOperator.Add, CoreRegister.Score, OperandSpec.FromRegister(CoreRegister.Value));

            EvaluatedOperation result = OperationEvaluator.EvaluateCoreLine(operation, Seeded(), new SourcePosition(7));

            Assert.AreEqual(CoreRegister.Score, result.Register);
            Assert.AreEqual(QuantityOperationType.Increase, result.OperationType);
            Assert.AreEqual(ExecutionEventSubtypes.QuantityChanged, result.Subtype);
            Assert.AreEqual(5, result.RequestedAmount);
            Assert.AreEqual(7, result.PriorValue);
            Assert.AreEqual(12, result.FinalValue);
            Assert.AreEqual(5, result.FinalDelta);
        }

        [Test]
        public void EvaluateInstruction_AddConstant_Computes()
        {
            QuantityChangeOperation operation = new(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromConstant(2));

            EvaluatedOperation result = OperationEvaluator.EvaluateInstruction(operation, Seeded(), new SourcePosition(2));

            Assert.AreEqual(QuantityOperationType.Increase, result.OperationType);
            Assert.AreEqual(ExecutionEventSubtypes.QuantityChanged, result.Subtype);
            Assert.AreEqual(2, result.RequestedAmount);
            Assert.AreEqual(5, result.PriorValue);
            Assert.AreEqual(7, result.FinalValue);
            Assert.AreEqual(2, result.FinalDelta);
        }

        [Test]
        public void EvaluateInstruction_MultiplyConstant_ComputesRequestedDelta()
        {
            QuantityChangeOperation operation = new(CoreRegister.Value, QuantityOperator.Multiply, OperandSpec.FromConstant(2));

            EvaluatedOperation result = OperationEvaluator.EvaluateInstruction(operation, Seeded(), new SourcePosition(2));

            Assert.AreEqual(QuantityOperationType.Increase, result.OperationType);
            Assert.AreEqual(ExecutionEventSubtypes.QuantityChanged, result.Subtype);
            Assert.AreEqual(5, result.PriorValue);
            Assert.AreEqual(10, result.FinalValue);
            Assert.AreEqual(5, result.RequestedAmount);
            Assert.AreEqual(5, result.FinalDelta);
        }

        [Test]
        public void EvaluateInstruction_LineNumberOperand_ResolvesToPosition()
        {
            QuantityChangeOperation operation = new(CoreRegister.Value, QuantityOperator.Add, OperandSpec.FromLineNumber());

            EvaluatedOperation result = OperationEvaluator.EvaluateInstruction(operation, Seeded(), new SourcePosition(4));

            Assert.AreEqual(4, result.RequestedAmount);
            Assert.AreEqual(5, result.PriorValue);
            Assert.AreEqual(9, result.FinalValue);
            Assert.AreEqual(4, result.FinalDelta);
        }

        [Test]
        public void Evaluate_DoesNotWriteRegisters()
        {
            ExecutionRegisters registers = Seeded();
            CoreLineOperation operation = new(CoreLineOperator.Assign, CoreRegister.Value, OperandSpec.FromConstant(99));

            OperationEvaluator.EvaluateCoreLine(operation, registers, new SourcePosition(1));

            Assert.AreEqual(5, registers.Value);
            Assert.AreEqual(2, registers.Signal);
            Assert.AreEqual(7, registers.Score);
        }

        [Test]
        public void EvaluateCoreLine_NullOperation_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = OperationEvaluator.EvaluateCoreLine(null, Seeded(), new SourcePosition(1)));
        }

        [Test]
        public void EvaluateInstruction_NullOperation_Throws()
        {
            Assert.Throws<ArgumentException>(() => _ = OperationEvaluator.EvaluateInstruction(null, Seeded(), new SourcePosition(1)));
        }
    }
}
