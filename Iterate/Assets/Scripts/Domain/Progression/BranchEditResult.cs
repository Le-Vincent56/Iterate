namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The typed outcome of one Branch inclusion or exclusion. A rejected edit changes nothing.
    /// </summary>
    /// <param name="Succeeded">Whether the edit was applied.</param>
    /// <param name="Rejection">The rejection reason; None on success.</param>
    public readonly record struct BranchEditResult(bool Succeeded, BranchEditRejection Rejection)
    {
        /// <summary>
        /// A successful result carrying no rejection.
        /// </summary>
        public static BranchEditResult Success { get; } = new BranchEditResult(true, BranchEditRejection.None);

        /// <summary>
        /// Creates a rejected result carrying a reason.
        /// </summary>
        /// <param name="rejection">The rejection reason.</param>
        /// <returns>The rejected result.</returns>
        public static BranchEditResult Rejected(BranchEditRejection rejection)
        {
            return new BranchEditResult(false, rejection);
        }
    }
}