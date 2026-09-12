namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The typed reason an archive was rejected.
    /// </summary>
    public enum ArchiveRejection
    {
        /// <summary>
        /// No rejection; the item was archived.
        /// </summary>
        None,

        /// <summary>
        /// The Buffer holds no item with that identity.
        /// </summary>
        UnknownInstance,

        /// <summary>
        /// No item is being held outside the Buffer, so there is nothing to archive.
        /// </summary>
        NothingIncoming
    }
}