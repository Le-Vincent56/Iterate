namespace Iterate.Domain.Content
{
    /// <summary>
    /// A target-lock-update operation: locks the effect's target to the unit its selection rule
    /// resolves when its observation window closes.
    /// </summary>
    /// <param name="Selection">The rule selecting the unit to lock.</param>
    public sealed record TargetLockUpdateOperation(TargetingRule Selection) : EffectOperation
    {
        /// <summary>
        /// The operation primitive this record represents.
        /// </summary>
        public override OperationKind Kind => OperationKind.TargetLockUpdate;
    }
}