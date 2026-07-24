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
    /// increase (the operation type records mechanical meaning, not arithmetic form). An accumulated
    /// modifier sum folds into an addition's resolved delta while the requested amount stays the
    /// unmodified request; assignments and multiplications ignore the sum — only fixed additions are
    /// modifiable at this content. A boundary effect resolves outside any source object, so it
    /// evaluates with no source position and admits constant operands only.
    /// </summary>
    public static class OperationEvaluator
    {
        /// <summary>
        /// Evaluates a Core-line operation against the current registers with no modifiers.
        /// </summary>
        /// <param name="operation">The Core-line operation to evaluate.</param>
        /// <param name="registers">The registers read during evaluation.</param>
        /// <param name="position">The executing source position, resolving a line-number operand.</param>
        /// <returns>The resolved evaluation result.</returns>
        /// <exception cref="ArgumentException">Thrown when the operation is null.</exception>
        public static EvaluatedOperation EvaluateCoreLine(CoreLineOperation operation, ExecutionRegisters registers, SourcePosition position)
        {
            return EvaluateCoreLine(operation, registers, position, 0);
        }

        /// <summary>
        /// Evaluates a Core-line operation against the current registers, folding the accumulated
        /// modifier sum into an addition's resolved delta.
        /// </summary>
        /// <param name="operation">The Core-line operation to evaluate.</param>
        /// <param name="registers">The registers read during evaluation.</param>
        /// <param name="position">The executing source position, resolving a line-number operand.</param>
        /// <param name="modifierSum">The accumulated modifier sum from the modification band.</param>
        /// <returns>The resolved evaluation result.</returns>
        /// <exception cref="ArgumentException">Thrown when the operation is null.</exception>
        public static EvaluatedOperation EvaluateCoreLine(
            CoreLineOperation operation,
            ExecutionRegisters registers,
            SourcePosition position,
            int modifierSum
        )
        {
            if (operation == null)
                throw new ArgumentException("A Core-line operation is required.", nameof(operation));

            return Evaluate(operation.Operator, operation.Target, operation.Operand, registers, position, modifierSum);
        }

        /// <summary>
        /// Evaluates an Instruction's quantity-change primary operation against the current registers
        /// with no modifiers.
        /// </summary>
        /// <param name="operation">The quantity-change operation to evaluate.</param>
        /// <param name="registers">The registers read during evaluation.</param>
        /// <param name="position">The executing source position, or null when the operation resolves outside any source object.</param>
        /// <returns>The resolved evaluation result.</returns>
        /// <exception cref="ArgumentException">Thrown when the operation is null or carries an unknown operator.</exception>
        public static EvaluatedOperation EvaluateInstruction(QuantityChangeOperation operation, ExecutionRegisters registers, SourcePosition? position)
        {
            return EvaluateInstruction(operation, registers, position, 0);
        }

        /// <summary>
        /// Evaluates an Instruction's quantity-change primary operation against the current registers,
        /// folding the accumulated modifier sum into an addition's resolved delta.
        /// </summary>
        /// <param name="operation">The quantity-change operation to evaluate.</param>
        /// <param name="registers">The registers read during evaluation.</param>
        /// <param name="position">The executing source position, or null when the operation resolves outside any source object.</param>
        /// <param name="modifierSum">The accumulated modifier sum from the modification band.</param>
        /// <returns>The resolved evaluation result.</returns>
        /// <exception cref="ArgumentException">Thrown when the operation is null, carries an unknown operator, or resolves a line-number operand with no position.</exception>
        public static EvaluatedOperation EvaluateInstruction(
            QuantityChangeOperation operation,
            ExecutionRegisters registers,
            SourcePosition? position,
            int modifierSum
        )
        {
            if (operation == null)
                throw new ArgumentException("A quantity-change operation is required.", nameof(operation));

            return Evaluate(ResolveOperator(operation), operation.Register, operation.Operand, registers, position, modifierSum);
        }

        /// <summary>
        /// Evaluates a boundary effect's quantity change outside any source object: no source
        /// position exists, so the operand must be a constant.
        /// </summary>
        /// <param name="operation">The quantity-change operation to evaluate.</param>
        /// <param name="registers">The registers read during evaluation.</param>
        /// <returns>The resolved evaluation result.</returns>
        /// <exception cref="ArgumentException">Thrown when the operation is null, carries an unknown operator, or resolves a non-constant operand.</exception>
        public static EvaluatedOperation EvaluateBoundaryEffect(QuantityChangeOperation operation, ExecutionRegisters registers)
        {
            if (operation == null)
                throw new ArgumentException("A quantity-change operation is required.", nameof(operation));

            if (operation.Operand.Source != OperandSource.Constant)
                throw new ArgumentException($"A boundary effect resolves outside any source object, so its operand must be a constant; '{operation.Operand.Source}' is not.", nameof(operation));

            return Evaluate(ResolveOperator(operation), operation.Register, operation.Operand, registers, null, 0);
        }

        /// <summary>
        /// Maps a quantity operator onto the Core-line operator the evaluator applies.
        /// </summary>
        /// <param name="operation">The quantity-change operation.</param>
        /// <returns>The resolved operator.</returns>
        /// <exception cref="ArgumentException">Thrown when the operator is not a known member.</exception>
        private static CoreLineOperator ResolveOperator(QuantityChangeOperation operation)
        {
            switch (operation.Operator)
            {
                case QuantityOperator.Add:
                    return CoreLineOperator.Add;

                case QuantityOperator.Multiply:
                    return CoreLineOperator.Multiply;

                default:
                    throw new ArgumentException($"Unknown quantity operator {operation.Operator}.", nameof(operation));
            }
        }

        /// <summary>
        /// Resolves the operand and applies the operator against the target register, classifying the
        /// resulting evidence without writing any register. An addition resolves to the operand plus
        /// the modifier sum; assignments and multiplications ignore the sum.
        /// </summary>
        /// <param name="op">The operator to apply.</param>
        /// <param name="target">The target register.</param>
        /// <param name="operand">The operand to resolve.</param>
        /// <param name="registers">The registers read during evaluation.</param>
        /// <param name="position">The executing source position, or null outside any source object.</param>
        /// <param name="modifierSum">The accumulated modifier sum.</param>
        /// <returns>The resolved evaluation result.</returns>
        /// <exception cref="ArgumentException">Thrown when the operator is not a known member.</exception>
        private static EvaluatedOperation Evaluate(
            CoreLineOperator op,
            CoreRegister target,
            OperandSpec operand,
            ExecutionRegisters registers,
            SourcePosition? position,
            int modifierSum
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
                    finalValue = prior + operandValue + modifierSum;
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
        /// <param name="position">The executing source position, or null outside any source object.</param>
        /// <returns>The resolved integer operand.</returns>
        /// <exception cref="ArgumentException">Thrown when the operand source is not a known member, or a line-number operand has no position.</exception>
        private static int ResolveOperand(OperandSpec operand, ExecutionRegisters registers, SourcePosition? position)
        {
            switch (operand.Source)
            {
                case OperandSource.Constant:
                    return operand.Constant;

                case OperandSource.Register:
                    return registers.Read(operand.SourceRegister);

                case OperandSource.LineNumber:
                    if (position == null)
                        throw new ArgumentException("A line-number operand requires a source position.", nameof(operand));

                    return position.Value.LineNumber;

                default:
                    throw new ArgumentException($"Unknown operand source {operand.Source}.", nameof(operand));
            }
        }
    }
}