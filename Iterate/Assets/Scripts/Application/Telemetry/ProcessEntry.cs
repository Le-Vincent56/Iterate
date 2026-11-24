using Iterate.Domain.Values;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// A Process entry Session Flow announced to its lifecycle.
    /// </summary>
    /// <param name="Process">The Process entered.</param>
    /// <param name="Route">The committed route, or null.</param>
    public readonly record struct ProcessEntry(ProcessID Process, RouteID? Route);
}