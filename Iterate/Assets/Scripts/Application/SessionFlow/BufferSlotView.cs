using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// One Instruction Buffer slot and the item in it, if any.
    /// </summary>
    /// <param name="Number">The one-based slot number.</param>
    /// <param name="Item">The item held, or null when empty.</param>
    /// <param name="DefinitionID">The held item's definition, or null when empty.</param>
    public sealed record BufferSlotView(int Number, InstanceID? Item, string DefinitionID);
}