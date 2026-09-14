namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One pinning action, kept in order so a shop's pin history reads without replaying it.
    /// </summary>
    /// <param name="Slot">The slot number.</param>
    /// <param name="Pinned">Whether it was pinned rather than unpinned.</param>
    public sealed record PinRecord(int Slot, bool Pinned);
}