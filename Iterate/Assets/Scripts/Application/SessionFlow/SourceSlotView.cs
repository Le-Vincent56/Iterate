using Iterate.Domain.Compilation;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// One source line: what kind of line it is and the item on it, if any.
    /// </summary>
    /// <param name="Line">The one-based line number.</param>
    /// <param name="Kind">What kind of line it is.</param>
    /// <param name="Item">The Instruction or Structure on the line, or null.</param>
    /// <param name="DefinitionID">That item's definition, or null.</param>
    public sealed record SourceSlotView(
        int Line,
        SourceSlotKind Kind,
        InstanceID? Item,
        string DefinitionID
    );
}