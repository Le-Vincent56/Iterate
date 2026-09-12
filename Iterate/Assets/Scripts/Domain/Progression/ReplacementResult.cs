namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The typed outcome of replacing an entry's item with a derived record: the record on success, or
    /// a rejection reason. A rejected replacement leaves the entry exactly as it was.
    /// </summary>
    /// <param name="Succeeded">Whether the item was replaced.</param>
    /// <param name="Rejection">The rejection reason; None on success.</param>
    /// <param name="Record">The written record; null on rejection.</param>
    public readonly record struct ReplacementResult(
        bool Succeeded,
        RepositoryRejection Rejection,
        ReplacementRecord Record
    )
    {
        /// <summary>
        /// Creates a successful result carrying the record.
        /// </summary>
        /// <param name="record">The written record.</param>
        /// <returns>The successful result.</returns>
        public static ReplacementResult Success(ReplacementRecord record)
        {
            return new ReplacementResult(true, RepositoryRejection.None, record);
        }

        /// <summary>
        /// Creates a rejected result carrying a reason.
        /// </summary>
        /// <param name="rejection">The rejection reason.</param>
        /// <returns>The rejected result.</returns>
        public static ReplacementResult Rejected(RepositoryRejection rejection)
        {
            return new ReplacementResult(false, rejection, null);
        }
    }
}