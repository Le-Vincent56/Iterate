namespace Iterate.Domain.Content
{
    /// <summary>
    /// A configuration-modification operation: adjusts or sets a named Process-setup configuration
    /// value.
    /// </summary>
    /// <param name="Setting">The configuration-setting token.</param>
    /// <param name="Amount">The signed adjustment, or the absolute value when setting.</param>
    /// <param name="SetsAbsolute">Whether the amount sets the value absolutely rather than adjusting it.</param>
    public sealed record ConfigurationModificationOperation(string Setting, int Amount, bool SetsAbsolute) : EffectOperation
    {
        /// <summary>
        /// The operation primitive this record represents.
        /// </summary>
        public override OperationKind Kind => OperationKind.ConfigurationModification;
    }
}