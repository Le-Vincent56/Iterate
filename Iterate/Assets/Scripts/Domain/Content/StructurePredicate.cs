namespace Iterate.Domain.Content
{
    /// <summary>
    /// A Condition Structure's predicate: a register compared against an operand. The comparison's
    /// runtime semantics belong to the Execution Engine; this record contributes the predicate as
    /// data.
    /// </summary>
    /// <param name="Register">The Core register the predicate reads.</param>
    /// <param name="Comparison">The comparison applied to the register.</param>
    /// <param name="Operand">The comparison operand; zero for parity comparisons.</param>
    public sealed record StructurePredicate(CoreRegister Register, PredicateComparison Comparison, int Operand);
}