using Iterate.Application.SessionFlow;

namespace Iterate.Application.Telemetry
{
    /// <summary>
    /// One command Session Flow has finished, with its result.
    /// </summary>
    /// <param name="Command">The command.</param>
    /// <param name="Result">Its result.</param>
    public readonly record struct CommandObservation(ISessionCommand Command, CommandResult Result);
}