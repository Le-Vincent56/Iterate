namespace Iterate.Domain.Content
{
    /// <summary>
    /// How a quantity-change operation combines its operand with a register. Serialized in JSON as
    /// ADD, MULTIPLY.
    /// </summary>
    public enum QuantityOperator
    {
        Add,
        Multiply
    }
}