namespace Iterate.Domain.Content
{
    /// <summary>
    /// A counter-request operation: adjusts a named counter by a signed delta, optionally clamped to
    /// a floor and ceiling.
    /// </summary>
    /// <param name="Counter">The counter name token.</param>
    /// <param name="Delta">The signed adjustment.</param>
    /// <param name="Floor">The clamp floor when <paramref name="HasFloor"/> is set.</param>
    /// <param name="Ceiling">The clamp ceiling when <paramref name="HasCeiling"/> is set.</param>
    /// <param name="HasFloor">Whether the floor clamp applies.</param>
    /// <param name="HasCeiling">Whether the ceiling clamp applies.</param>
    public sealed record CounterRequestOperation(
        string Counter,
        int Delta,
        int Floor,
        int Ceiling,
        bool HasFloor,
        bool HasCeiling
    ) : EffectOperation
    {
        /// <summary>
        /// The operation primitive this record represents.
        /// </summary>
        public override OperationKind Kind => OperationKind.CounterRequest;
    }
}