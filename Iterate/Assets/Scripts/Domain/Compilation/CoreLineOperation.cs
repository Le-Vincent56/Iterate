using System;
using Iterate.Domain.Content;

namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// A typed Core-line operation payload: an operator applied to a target Core register with a single
    /// operand resolved at execution. Replaces the opaque operation text a Core line once carried, so
    /// the Execution Engine evaluates Core lines as data with no parsing and no dual truth.
    /// </summary>
    /// <param name="Operator">The operator applied to the target register.</param>
    /// <param name="Target">The Core register the operation writes.</param>
    /// <param name="Operand">The operand resolved and applied to the target; non-null.</param>
    public sealed record CoreLineOperation(CoreLineOperator Operator, CoreRegister Target, OperandSpec Operand)
    {
        /// <summary>
        /// The operand resolved and applied to the target register. Validated non-null at construction.
        /// </summary>
        public OperandSpec Operand { get; } = RequireOperand(Operand);

        /// <summary>
        /// Validates that the operand is present.
        /// </summary>
        /// <param name="operand">The candidate operand.</param>
        /// <returns>The operand unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the operand is null.</exception>
        private static OperandSpec RequireOperand(OperandSpec operand)
        {
            if (operand == null)
                throw new ArgumentException("A CoreLineOperation requires an operand.", nameof(operand));

            return operand;
        }
    }
}