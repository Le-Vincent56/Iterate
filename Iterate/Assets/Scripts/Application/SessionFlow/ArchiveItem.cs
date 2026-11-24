using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Archives an Instruction Buffer item for the rest of the Process, or the incoming item that is
    /// holding the Buffer in overflow.
    /// </summary>
    /// <param name="Item">The Buffer item to archive; null archives the incoming item.</param>
    public sealed record ArchiveItem(InstanceID? Item) : ISessionCommand;
}