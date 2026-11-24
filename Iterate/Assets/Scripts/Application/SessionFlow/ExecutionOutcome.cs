using Iterate.Domain.Compilation;
using Iterate.Domain.Progression;
using Iterate.Domain.Trace;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// What one compile-and-execute produced: the committed compilation, the frozen execution record,
    /// and its evaluation against the Process thresholds.
    /// </summary>
    /// <param name="Attempt">The committed compilation attempt.</param>
    /// <param name="Record">The execution record.</param>
    /// <param name="Evaluation">The record's evaluation.</param>
    public sealed record ExecutionOutcome(CompilationAttempt Attempt, ExecutionRecord Record, ThresholdEvaluation Evaluation);
}