namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The outcome of archiving a buffered or incoming item. A rejected archive changes nothing.
    /// </summary>
    /// <param name="Succeeded">Whether the item was archived.</param>
    /// <param name="Rejection">The rejection reason; None on success.</param>
    /// <param name="Item">The archived item; null on rejection.</param>
    public readonly record struct ArchiveResult(bool Succeeded, ArchiveRejection Rejection, RepositoryItem Item)
    {
        /// <summary>
        /// Creates a successful result carrying the archived item.
        /// </summary>
        /// <param name="item">The archived item.</param>
        /// <returns>The archive result.</returns>
        public static ArchiveResult Success(RepositoryItem item)
        {
            return new ArchiveResult(true, ArchiveRejection.None, item);
        }

        /// <summary>
        /// Creates a rejected result carrying a reason.
        /// </summary>
        /// <param name="rejection">The rejection reason.</param>
        /// <returns>The archive result.</returns>
        public static ArchiveResult Rejected(ArchiveRejection rejection)
        {
            return new ArchiveResult(false, rejection, null);
        }
    }
}