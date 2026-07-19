namespace Iterate.Domain.Content
{
    /// <summary>
    /// A rescue operation: transforms a rescuable unit's disposition to a resulting disposition
    /// without creating a new unit or resetting allowances.
    /// </summary>
    /// <param name="ResultingDisposition">The disposition token the rescued unit resolves to.</param>
    public sealed record RescueOperation(string ResultingDisposition) : EffectOperation
    {
        /// <summary>
        /// The operation primitive this record represents.
        /// </summary>
        public override OperationKind Kind => OperationKind.Rescue;
    }
}