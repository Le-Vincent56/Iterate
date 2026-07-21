namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// The closed set of operators a Core line may apply to its target register: assign a resolved
    /// operand, add it, or multiply by it. Core-specific and deliberately distinct from the catalog's
    /// quantity-change operator vocabulary and from the Trace's evidence-classifying operation types.
    /// </summary>
    public enum CoreLineOperator
    {
        Assign,
        Add,
        Multiply
    }
}