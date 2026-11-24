using Iterate.Domain.Compilation;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Exchanges the lines at two source positions.
    /// </summary>
    /// <param name="First">One position.</param>
    /// <param name="Second">The other position.</param>
    public sealed record SwapLines(SourcePosition First, SourcePosition Second) : ISessionCommand;
}