namespace Iterate.Domain.Content
{
    /// <summary>
    /// An added-execution-request operation: schedules an added execution against a target, optionally
    /// cancelling when the target is invalid at resolution.
    /// </summary>
    /// <param name="Target">The rule selecting the added execution's target.</param>
    /// <param name="CancelOnInvalid">Whether an invalid target cancels the request.</param>
    public sealed record AddedExecutionRequestOperation(TargetingRule Target, bool CancelOnInvalid) : EffectOperation
    {
        /// <summary>
        /// The operation primitive this record represents.
        /// </summary>
        public override OperationKind Kind => OperationKind.AddedExecutionRequest;
    }
}