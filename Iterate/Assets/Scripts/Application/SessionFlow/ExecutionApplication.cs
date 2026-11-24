using Iterate.Domain.Progression;
using Iterate.Domain.Trace;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The state hand-back step and the only writer of execution outcomes to live state. It records
    /// the execution against the Process counters, evaluates the record against the Process
    /// thresholds, and holds the result for review. Nothing else carries over from an execution in
    /// this slice: Heat starts from zero every execution, and no content grants an execution
    /// opportunity.
    /// </summary>
    internal static class ExecutionApplication
    {
        /// <summary>
        /// Applies one execution's outcome.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="record">The frozen execution record.</param>
        /// <returns>The record's evaluation.</returns>
        internal static ThresholdEvaluation Apply(SessionContext context, ExecutionRecord record)
        {
            ProcessState process = context.Process;
            process.Counters.RecordExecution();
            ThresholdEvaluation evaluation = ThresholdEvaluator.Evaluate(record, process.Configuration.Thresholds);
            context.Result = new CurrentResult(record, evaluation, false);
            return evaluation;
        }
    }
}