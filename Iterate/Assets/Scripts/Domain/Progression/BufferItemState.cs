namespace Iterate.Domain.Progression
{
    /// <summary>
    /// What became of an item the Buffer once held. Archived and Consumed both leave active capacity
    /// rather than lingering as disabled slots, and they are semantically distinct: an archived item
    /// is unavailable for the rest of the Process and returns to the Repository afterward, while a
    /// consumed Directive spent itself creating its pragma.
    /// </summary>
    public enum BufferItemState
    {
        /// <summary>
        /// The item is not one the Buffer has held.
        /// </summary>
        None,

        /// <summary>
        /// The item occupies a slot.
        /// </summary>
        Present,

        /// <summary>
        /// The item was archived: unavailable for the rest of the Process, returned afterward.
        /// </summary>
        Archived,

        /// <summary>
        /// The Directive was activated and spent.
        /// </summary>
        Consumed
    }
}