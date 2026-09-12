namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The typed reason a Repository operation was rejected. Legal-play failures are these values,
    /// never exceptions; a rejected operation changes nothing at all.
    /// </summary>
    public enum RepositoryRejection
    {
        /// <summary>
        /// No rejection; the operation succeeded.
        /// </summary>
        None,

        /// <summary>
        /// The definition is not one of the three Repository item kinds.
        /// </summary>
        NotAnItem,

        /// <summary>
        /// The named instance is not in the Repository.
        /// </summary>
        UnknownInstance,

        /// <summary>
        /// The entry is a starter and cannot be deleted.
        /// </summary>
        StarterProtected,

        /// <summary>
        /// The replacement carries a different identity or definition than the entry it would replace.
        /// </summary>
        DefinitionMismatch
    }
}