namespace Iterate.Domain.Content
{
    /// <summary>
    /// A disposition-change operation: sets a runtime unit's disposition to a controlled outcome.
    /// </summary>
    /// <param name="NewDisposition">The resulting disposition token.</param>
    public sealed record DispositionChangeOperation(string NewDisposition) : EffectOperation
    {
        /// <summary>
        /// The operation primitive this record represents.
        /// </summary>
        public override OperationKind Kind => OperationKind.DispositionChange;
    }
}