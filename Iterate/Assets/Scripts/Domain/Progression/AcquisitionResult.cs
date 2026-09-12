namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The typed outcome of acquiring an item: the entry and its record on success, or a rejection
    /// reason. A rejected acquisition allocates no identity and no suffix and writes no record.
    /// </summary>
    /// <param name="Succeeded">Whether the item was acquired.</param>
    /// <param name="Rejection">The rejection reason; None on success.</param>
    /// <param name="Entry">The created entry; null on rejection.</param>
    /// <param name="Record">The written record; null on rejection.</param>
    public readonly record struct AcquisitionResult(
        bool Succeeded,
        RepositoryRejection Rejection,
        RepositoryEntry Entry,
        AcquisitionRecord Record
    )
    {
        /// <summary>
        /// Creates a successful result carrying the entry and its record.
        /// </summary>
        /// <param name="entry">The created entry.</param>
        /// <param name="record">The written record.</param>
        /// <returns>The successful result.</returns>
        public static AcquisitionResult Success(RepositoryEntry entry, AcquisitionRecord record)
        {
            return new AcquisitionResult(true, RepositoryRejection.None, entry, record);
        }

        /// <summary>
        /// Creates a rejected result carrying a reason.
        /// </summary>
        /// <param name="rejection">The rejection reason.</param>
        /// <returns>The rejected result.</returns>
        public static AcquisitionResult Rejected(RepositoryRejection rejection)
        {
            return new AcquisitionResult(false, rejection, null, null);
        }
    }
}