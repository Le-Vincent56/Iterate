namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The moment a scripted load arrives: after a named execution. Result Review resolves before an
    /// arrival, and nothing arrives after the final execution.
    /// </summary>
    /// <param name="AfterExecution">The execution this moment follows.</param>
    public readonly record struct ArrivalMoment(int AfterExecution);
}