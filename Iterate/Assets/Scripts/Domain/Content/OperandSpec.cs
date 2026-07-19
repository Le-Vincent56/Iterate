namespace Iterate.Domain.Content
{
    /// <summary>
    /// Specifies an operand for a quantity-change operation: a literal constant, a Core register
    /// read, or the executing source line's number.
    /// </summary>
    /// <param name="Source">Which operand source this spec selects.</param>
    /// <param name="Constant">The literal value when the source is a constant; otherwise zero.</param>
    /// <param name="SourceRegister">The register read when the source is a register; otherwise Value.</param>
    public sealed record OperandSpec(OperandSource Source, int Constant, CoreRegister SourceRegister)
    {
        /// <summary>
        /// Creates an operand that reads a literal constant.
        /// </summary>
        /// <param name="constant">The literal value.</param>
        /// <returns>The constant operand spec.</returns>
        public static OperandSpec FromConstant(int constant) => new(OperandSource.Constant, constant, CoreRegister.Value);

        /// <summary>
        /// Creates an operand that reads a Core register.
        /// </summary>
        /// <param name="register">The register to read.</param>
        /// <returns>The register operand spec.</returns>
        public static OperandSpec FromRegister(CoreRegister register) => new(OperandSource.Register, 0, register);

        /// <summary>
        /// Creates an operand that reads the executing source line's number.
        /// </summary>
        /// <returns>The line-number operand spec.</returns>
        public static OperandSpec FromLineNumber() => new(OperandSource.LineNumber, 0, CoreRegister.Value);
    }
}