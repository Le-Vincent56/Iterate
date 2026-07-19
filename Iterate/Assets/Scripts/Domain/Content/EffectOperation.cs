namespace Iterate.Domain.Content
{
    /// <summary>
    /// The base of the closed effect-operation set: one sealed derived record per primitive in the
    /// TA-DAT-003 operation set (a C# 10 sealed-hierarchy stand-in for a discriminated union). Each
    /// derived record carries its typed payload and reports its <see cref="OperationKind"/>.
    /// </summary>
    public abstract record EffectOperation
    {
        /// <summary>
        /// The operation primitive this record represents.
        /// </summary>
        public abstract OperationKind Kind { get; }
    }
}