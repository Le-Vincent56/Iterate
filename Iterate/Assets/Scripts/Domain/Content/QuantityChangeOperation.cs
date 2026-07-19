namespace Iterate.Domain.Content
{
    /// <summary>
    /// A quantity-change operation: applies an operator and operand to a Core register.
    /// </summary>
    /// <param name="Register">The Core register the operation writes.</param>
    /// <param name="Operator">Whether the operand is added or multiplied.</param>
    /// <param name="Operand">The operand source and value.</param>
    public sealed record QuantityChangeOperation(
        CoreRegister Register,
        QuantityOperator Operator,
        OperandSpec Operand
    ) : EffectOperation
    {
        /// <summary>
        /// The operation primitive this record represents.
        /// </summary>
        public override OperationKind Kind => OperationKind.QuantityChange;
    }
}