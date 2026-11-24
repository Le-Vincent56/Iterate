using Iterate.Domain.Compilation;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Removes the line at a source position, returning its item to the Instruction Buffer.
    /// </summary>
    /// <param name="Position">The source position to clear.</param>
    public sealed record RemoveLine(SourcePosition Position) : ISessionCommand;
}