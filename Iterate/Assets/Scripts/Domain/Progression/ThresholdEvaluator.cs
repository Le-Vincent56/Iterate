using System;
using Iterate.Domain.Content;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Decides which reward tier a Process's final output reached. Only the designated final output
    /// counts, not the Score register it usually equals, and only a valid result counts at all.
    /// </summary>
    public static class ThresholdEvaluator
    {
        /// <summary>
        /// Evaluates a final state against a Process's thresholds. Takes the values rather than the
        /// record so a fixture can evaluate a hand-built outcome.
        /// </summary>
        /// <param name="final">The execution's final state.</param>
        /// <param name="validity">Whether the result is valid.</param>
        /// <param name="thresholds">The Process's thresholds.</param>
        /// <returns>The evaluation.</returns>
        public static ThresholdEvaluation Evaluate(
            FinalExecutionState final,
            ResultValidityStatus validity,
            ProcessThresholdSpec thresholds
        )
        {
            if (final == null)
                throw new ArgumentException("An evaluation requires a final state.", nameof(final));

            if (thresholds == null)
                throw new ArgumentException("An evaluation requires thresholds.", nameof(thresholds));

            if (validity != ResultValidityStatus.Valid)
                return new ThresholdEvaluation(false, null, ThresholdRejection.ResultInvalid);

            int output = final.FinalOutput.Value;
            if (output >= thresholds.Benchmark)
                return new ThresholdEvaluation(true, RewardTier.Benchmark, ThresholdRejection.None);

            if (output >= thresholds.Optimize)
                return new ThresholdEvaluation(true, RewardTier.Optimize, ThresholdRejection.None);

            if (output >= thresholds.Pass)
                return new ThresholdEvaluation(true, RewardTier.Pass, ThresholdRejection.None);

            return new ThresholdEvaluation(true, null, ThresholdRejection.None);
        }

        /// <summary>
        /// Evaluates an execution record. Preferred over the value overload wherever a record exists:
        /// result validity is derived from the record's own completion and trace completeness, so
        /// forwarding it by hand is a chance to pair a valid state with the wrong validity.
        /// </summary>
        /// <param name="record">The execution record.</param>
        /// <param name="thresholds">The Process's thresholds.</param>
        /// <returns>The evaluation.</returns>
        public static ThresholdEvaluation Evaluate(ExecutionRecord record, ProcessThresholdSpec thresholds)
        {
            if (record == null)
                throw new ArgumentException("An evaluation requires an execution record.", nameof(record));

            return Evaluate(record.FinalState, record.ResultValidity, thresholds);
        }
    }
}