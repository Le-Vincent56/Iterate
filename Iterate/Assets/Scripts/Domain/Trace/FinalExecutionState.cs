using Iterate.Domain.Values;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The register state produced by an execution: the final Value, Signal, and Score registers and the
    /// final output. These quantities are preserved independently of the result's validity — an invalid
    /// execution still carries the numbers it produced.
    /// </summary>
    public sealed record FinalExecutionState
    {
        /// <summary>
        /// The Value register at execution end.
        /// </summary>
        public ValueAmount FinalValue { get; }

        /// <summary>
        /// The Signal register at execution end.
        /// </summary>
        public SignalValue FinalSignal { get; }

        /// <summary>
        /// The Score register at execution end.
        /// </summary>
        public ScoreValue FinalScore { get; }

        /// <summary>
        /// The final output produced by the execution.
        /// </summary>
        public ScoreValue FinalOutput { get; }

        public FinalExecutionState(
            ValueAmount finalValue,
            SignalValue finalSignal,
            ScoreValue finalScore,
            ScoreValue finalOutput
        )
        {
            FinalValue = finalValue;
            FinalSignal = finalSignal;
            FinalScore = finalScore;
            FinalOutput = finalOutput;
        }
    }
}