namespace Iterate.Domain.Compilation
{
    /// <summary>
    /// The typed outcome of applying a source edit: success, or a rejection reason from the game
    /// vocabulary. Exceptions are reserved for contract violations, never legal-play failures.
    /// </summary>
    /// <param name="Succeeded">Whether the edit was applied.</param>
    /// <param name="Rejection">The rejection reason; <see cref="SourceEditRejection.None"/> on success.</param>
    public readonly record struct BuildOperationResult(bool Succeeded, SourceEditRejection Rejection)
    {
        /// <summary>
        /// A successful result carrying no rejection.
        /// </summary>
        public static BuildOperationResult Success { get; } = new BuildOperationResult(true, SourceEditRejection.None);

        /// <summary>
        /// Creates a rejected result carrying a reason.
        /// </summary>
        /// <param name="rejection">The rejection reason.</param>
        /// <returns>A failed result carrying the reason.</returns>
        public static BuildOperationResult Rejected(SourceEditRejection rejection)
        {
            return new BuildOperationResult(false, rejection);
        }
    }
}