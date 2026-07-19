namespace Iterate.Domain.Content
{
    /// <summary>
    /// A prediction-visibility operation: grants a controlled prediction projection before execution.
    /// </summary>
    /// <param name="Projection">The prediction-projection token.</param>
    public sealed record PredictionVisibilityOperation(string Projection) : EffectOperation
    {
        /// <summary>
        /// The operation primitive this record represents.
        /// </summary>
        public override OperationKind Kind => OperationKind.PredictionVisibility;
    }
}