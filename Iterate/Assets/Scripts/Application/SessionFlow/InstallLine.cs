using Iterate.Domain.Compilation;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Installs an Instruction Buffer item at a source position, overwriting the line there when the
    /// position is occupied.
    /// </summary>
    /// <param name="Item">The item to install.</param>
    /// <param name="Position">The source position to install it at.</param>
    public sealed record InstallLine(InstanceID Item, SourcePosition Position) : ISessionCommand;
}