using Iterate.Application.SessionFlow;
using Iterate.Domain.Values;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// A Process failure Session Flow announced to its lifecycle, with the lifecycle's answer.
    /// </summary>
    /// <param name="Process">The Process that failed.</param>
    /// <param name="Route">The committed route, or null.</param>
    /// <param name="Disposition">What the lifecycle decided happens next.</param>
    public readonly record struct ProcessFailure(ProcessID Process, RouteID? Route, ProcessFailureDisposition Disposition);
}