using Iterate.Domain.Values;

namespace Iterate.Domain.Trace
{
    /// <summary>
    /// The register state captured at the start of an execution, from which the run can be reset for
    /// reproduction. Later Epic children extend this additively as more reset state is recorded.
    /// </summary>
    public sealed record InitialExecutionState
    {
        /// <summary>
        /// The Value register at execution start.
        /// </summary>
        public ValueAmount InitialValue { get; }

        /// <summary>
        /// The Signal register at execution start.
        /// </summary>
        public SignalValue InitialSignal { get; }

        /// <summary>
        /// The Score register at execution start.
        /// </summary>
        public ScoreValue InitialScore { get; }

        public InitialExecutionState(ValueAmount initialValue, SignalValue initialSignal, ScoreValue initialScore)
        {
            InitialValue = initialValue;
            InitialSignal = initialSignal;
            InitialScore = initialScore;
        }
    }
}