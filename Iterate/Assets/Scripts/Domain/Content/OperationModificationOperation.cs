namespace Iterate.Domain.Content
{
    /// <summary>
    /// An operation-modification operation: adjusts a host operation's fixed operand by a signed
    /// delta.
    /// </summary>
    /// <param name="OperandDelta">The signed adjustment to the host operation's operand.</param>
    public sealed record OperationModificationOperation(int OperandDelta) : EffectOperation
    {
        /// <summary>
        /// The operation primitive this record represents.
        /// </summary>
        public override OperationKind Kind => OperationKind.OperationModification;
    }
}