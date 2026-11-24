using Iterate.Domain.Compilation;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Moves the line at one source position to another.
    /// </summary>
    /// <param name="Origin">The position the line leaves.</param>
    /// <param name="Destination">The position the line arrives at.</param>
    public sealed record MoveLine(SourcePosition Origin, SourcePosition Destination) : ISessionCommand;
}