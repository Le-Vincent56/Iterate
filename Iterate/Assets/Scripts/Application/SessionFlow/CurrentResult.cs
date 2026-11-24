using Iterate.Domain.Progression;
using Iterate.Domain.Trace;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The execution result under review: the frozen record, its threshold evaluation, and whether
    /// the player discarded it to keep building.
    /// </summary>
    /// <param name="Record">The execution record.</param>
    /// <param name="Evaluation">The record's evaluation against the Process thresholds.</param>
    /// <param name="Discarded">Whether the result was discarded.</param>
    public sealed record CurrentResult(ExecutionRecord Record, ThresholdEvaluation Evaluation, bool Discarded);
}