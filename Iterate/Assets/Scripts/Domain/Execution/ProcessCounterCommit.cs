namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The outcome of one committed Process-counter request: what the counter held, what was asked,
    /// what was actually applied after clamping, and where it landed. The requested and final deltas
    /// differ exactly when a declared bound reduced the change, which is the evidence that a bound
    /// was reached.
    /// </summary>
    /// <param name="PriorValue">The counter value before the change.</param>
    /// <param name="RequestedDelta">The delta the request declared.</param>
    /// <param name="FinalDelta">The delta actually applied after clamping.</param>
    /// <param name="FinalValue">The counter value after the change.</param>
    /// <param name="BoundApplied">Whether a declared bound reduced the requested delta.</param>
    public readonly record struct ProcessCounterCommit(
        int PriorValue,
        int RequestedDelta,
        int FinalDelta,
        int FinalValue,
        bool BoundApplied
    );
}