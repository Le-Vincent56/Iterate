namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The typed outcome of recording an execution. A refused record never advances the count.
    /// </summary>
    /// <param name="Succeeded">Whether the execution was recorded.</param>
    /// <param name="ExecutionsRun">The executions run after the attempt.</param>
    /// <param name="ExecutionsRemaining">The executions still allowed after the attempt.</param>
    public readonly record struct CounterResult(bool Succeeded, int ExecutionsRun, int ExecutionsRemaining);
}