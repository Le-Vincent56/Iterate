using System;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The pure evaluator that resolves one operation against the current registers into an
    /// <see cref="EvaluatedOperation"/>. It reads registers but never writes them, so the scheduler owns
    /// all mutation. Assign classifies as an absolute assignment; Add and Multiply both classify as an
    /// increase (the operation type records mechanical meaning, not arithmetic form).
    /// </summary>
    public static class OperationEvaluator
    {
        /// <summary>
        /// Evaluates a Core-line operation against the current registers.
        /// </summary>
        /// <param name="operation">The Core-line operation to evaluate.</param>
        /// <param name="registers">The registers read during evaluation.</param>
        /// <param name="position">The executing source position, resolving a line-number operand.</param>
        /// <returns>The resolved evaluation result.</returns>
        /// <exception cref="ArgumentException">Thrown when the operation is null.</exception>
        public static EvaluatedOperation EvaluateCoreLine(CoreLineOperation operation, ExecutionRegisters registers, SourcePosition position)
        {
            if (operation == null)
                throw new ArgumentException("A Core-line operation is required.", nameof(operation));

            return Evaluate(operation.Operator, operation.Target, operation.Operand, registers, position);
        }

        /// <summary>
        /// Evaluates an Instruction's quantity-change primary operation against the current registers.
        /// </summary>
        /// <param name="operation">The quantity-change operation to evaluate.</param>
        /// <param name="registers">The registers read during evaluation.</param>
        /// <param name="position">The executing source position, resolving a line-number operand.</param>
        /// <returns>The resolved evaluation result.</returns>
        /// <exception cref="ArgumentException">Thrown when the operation is null or carries an unknown operator.</exception>
        public static EvaluatedOperation EvaluateInstruction(QuantityChangeOperation operation, ExecutionRegisters registers, SourcePosition position)
        {
            if (operation == null)
                throw new ArgumentException("A quantity-change operation is required.", nameof(operation));

            CoreLineOperator resolvedOperator;
            switch (operation.Operator)
            {
                case QuantityOperator.Add:
                    resolvedOperator = CoreLineOperator.Add;
                    break;
                
                case QuantityOperator.Multiply:
                    resolvedOperator = CoreLineOperator.Multiply;
                    break;
                
                default:
                    throw new ArgumentException($"Unknown quantity operator {operation.Operator}.", nameof(operation));
            }

            return Evaluate(resolvedOperator, operation.Register, operation.Operand, registers, position);
        }

        /// <summary>
        /// Resolves the operand and applies the operator against the target register, classifying the
        /// resulting evidence without writing any register.
        /// </summary>
        /// <param name="op">The operator to apply.</param>
        /// <param name="target">The target register.</param>
        /// <param name="operand">The operand to resolve.</param>
        /// <param name="registers">The registers read during evaluation.</param>
        /// <param name="position">The executing source position.</param>
        /// <returns>The resolved evaluation result.</returns>
        /// <exception cref="ArgumentException">Thrown when the operator is not a known member.</exception>
        private static EvaluatedOperation Evaluate(
            CoreLineOperator op,
            CoreRegister target,
            OperandSpec operand,
            ExecutionRegisters registers,
            SourcePosition position
        )
        {
            int operandValue = ResolveOperand(operand, registers, position);
            int prior = registers.Read(target);

            int finalValue;
            QuantityOperationType operationType;
            string subtype;
            int requested;
            switch (op)
            {
                case CoreLineOperator.Assign:
                    finalValue = operandValue;
                    operationType = QuantityOperationType.Assign;
                    subtype = ExecutionEventSubtypes.QuantityAssigned;
                    requested = operandValue;
                    break;
                
                case CoreLineOperator.Add:
                    finalValue = prior + operandValue;
                    operationType = QuantityOperationType.Increase;
                    subtype = ExecutionEventSubtypes.QuantityChanged;
                    requested = operandValue;
                    break;
                
                case CoreLineOperator.Multiply:
                    finalValue = prior * operandValue;
                    operationType = QuantityOperationType.Increase;
                    subtype = ExecutionEventSubtypes.QuantityChanged;
                    requested = (prior * operandValue) - prior;
                    break;
                
                default:
                    throw new ArgumentException($"Unknown Core-line operator {op}.", nameof(op));
            }

            return new EvaluatedOperation(target, operationType, subtype, requested, prior, finalValue, finalValue - prior);
        }

        /// <summary>
        /// Resolves an operand to its integer value: a literal constant, the current value of a register,
        /// or the executing source line number.
        /// </summary>
        /// <param name="operand">The operand to resolve.</param>
        /// <param name="registers">The registers read for a register operand.</param>
        /// <param name="position">The executing source position for a line-number operand.</param>
        /// <returns>The resolved integer operand.</returns>
        /// <exception cref="ArgumentException">Thrown when the operand source is not a known member.</exception>
        private static int ResolveOperand(OperandSpec operand, ExecutionRegisters registers, SourcePosition position)
        {
            switch (operand.Source)
            {
                case OperandSource.Constant:
                    return operand.Constant;
                
                case OperandSource.Register:
                    return registers.Read(operand.SourceRegister);
                
                case OperandSource.LineNumber:
                    return position.LineNumber;
                
                default:
                    throw new ArgumentException($"Unknown operand source {operand.Source}.", nameof(operand));
            }
        }
    }
}