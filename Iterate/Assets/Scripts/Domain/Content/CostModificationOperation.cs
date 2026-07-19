namespace Iterate.Domain.Content
{
    /// <summary>
    /// A cost-modification operation: adjusts or sets a named cost, clamped to a floor, and records
    /// whether progression still advances.
    /// </summary>
    /// <param name="CostKind">The cost kind token.</param>
    /// <param name="SetsAbsolute">Whether the amount sets the cost absolutely rather than adjusting it.</param>
    /// <param name="Amount">The signed adjustment, or the absolute value when setting.</param>
    /// <param name="Floor">The clamp floor for the resulting cost.</param>
    /// <param name="ProgressionAdvances">Whether progression advances despite the modified cost.</param>
    public sealed record CostModificationOperation(
        string CostKind,
        bool SetsAbsolute,
        int Amount,
        int Floor,
        bool ProgressionAdvances
    ) : EffectOperation
    {
        /// <summary>
        /// The operation primitive this record represents.
        /// </summary>
        public override OperationKind Kind => OperationKind.CostModification;
    }
}