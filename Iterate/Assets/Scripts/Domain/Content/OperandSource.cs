namespace Iterate.Domain.Content
{
    /// <summary>
    /// Where a quantity-change operand reads its value. Serialized in JSON as CONSTANT, REGISTER,
    /// LINE_NUMBER.
    /// </summary>
    public enum OperandSource
    {
        Constant,
        Register,
        LineNumber
    }
}