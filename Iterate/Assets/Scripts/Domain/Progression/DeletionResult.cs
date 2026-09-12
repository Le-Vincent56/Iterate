namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The typed outcome of deleting an item: the record on success, or a rejection reason. A rejected
    /// deletion leaves the Repository exactly as it was.
    /// </summary>
    /// <param name="Succeeded">Whether the item was deleted.</param>
    /// <param name="Rejection">The rejection reason; None on success.</param>
    /// <param name="Record">The written record; null on rejection.</param>
    public readonly record struct DeletionResult(
        bool Succeeded,
        RepositoryRejection Rejection,
        DeletionRecord Record
    )
    {
        /// <summary>
        /// Creates a successful result carrying the record.
        /// </summary>
        /// <param name="record">The written record.</param>
        /// <returns>The successful result.</returns>
        public static DeletionResult Success(DeletionRecord record)
        {
            return new DeletionResult(true, RepositoryRejection.None, record);
        }

        /// <summary>
        /// Creates a rejected result carrying a reason.
        /// </summary>
        /// <param name="rejection">The rejection reason.</param>
        /// <returns>The rejected result.</returns>
        public static DeletionResult Rejected(RepositoryRejection rejection)
        {
            return new DeletionResult(false, rejection, null);
        }
    }
}