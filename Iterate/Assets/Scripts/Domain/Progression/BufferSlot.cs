namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One numbered Buffer slot. Slots are fixed for the Process's whole life and an item keeps its
    /// slot until it leaves, so a slot number is a stable handle the interface can point at. Slot
    /// position carries no mechanical meaning.
    /// </summary>
    /// <param name="Number">The one-based slot number.</param>
    /// <param name="Item">The item occupying the slot, or null when the slot is open.</param>
    public sealed record BufferSlot(int Number, RepositoryItem Item);
}