using System;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// A Process's execution counters: how many executions it allows, how many have run, and — when
    /// the Process makes them mandatory — how many the player still owes before the Process may end.
    /// A Process that allows no more executions refuses to record another rather than silently
    /// over-running its allowance.
    /// </summary>
    public sealed class ProcessCounters
    {
        /// <summary>
        /// The authored execution allowance.
        /// </summary>
        public int Allowance { get; }

        /// <summary>
        /// Whether every allowed execution must be run before the Process may end.
        /// </summary>
        public bool MandatoryExecutions { get; }

        /// <summary>
        /// How many executions have run.
        /// </summary>
        public int ExecutionsRun { get; private set; }

        /// <summary>
        /// How many executions the allowance still permits.
        /// </summary>
        public int ExecutionsRemaining => Allowance - ExecutionsRun;

        /// <summary>
        /// Whether another execution may run.
        /// </summary>
        public bool CanExecute => ExecutionsRemaining > 0;

        /// <summary>
        /// How many executions the player still owes; always zero when executions are optional.
        /// </summary>
        public int MandatoryExecutionsRemaining => MandatoryExecutions ? ExecutionsRemaining : 0;

        public ProcessCounters(int allowance, bool mandatoryExecutions)
        {
            if (allowance < 1)
                throw new ArgumentException("A Process must allow at least one execution.", nameof(allowance));

            Allowance = allowance;
            MandatoryExecutions = mandatoryExecutions;
        }

        /// <summary>
        /// Records one execution against the allowance.
        /// </summary>
        /// <returns>The counter result.</returns>
        public CounterResult RecordExecution()
        {
            if (!CanExecute)
                return new CounterResult(false, ExecutionsRun, ExecutionsRemaining);

            ExecutionsRun++;
            return new CounterResult(true, ExecutionsRun, ExecutionsRemaining);
        }
    }
}