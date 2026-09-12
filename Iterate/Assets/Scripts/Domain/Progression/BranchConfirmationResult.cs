namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The typed outcome of confirming a draft: the record on success, or the validation explaining
    /// why not. A rejected confirmation leaves both the draft and the confirmed set untouched, so the
    /// player can keep editing.
    /// </summary>
    /// <param name="Succeeded">Whether the draft was confirmed.</param>
    /// <param name="Validation">The validation the confirmation was judged against.</param>
    /// <param name="Record">The written record; null on rejection.</param>
    public readonly record struct BranchConfirmationResult(
        bool Succeeded,
        BranchValidation Validation,
        BranchConfirmationRecord Record
    )
    {
        /// <summary>
        /// Creates a successful result carrying the validation and the record.
        /// </summary>
        /// <param name="validation">The satisfied validation.</param>
        /// <param name="record">The written record.</param>
        /// <returns>The successful result.</returns>
        public static BranchConfirmationResult Success(BranchValidation validation, BranchConfirmationRecord record)
        {
            return new BranchConfirmationResult(true, validation, record);
        }

        /// <summary>
        /// Creates a rejected result carrying the validation that blocked it.
        /// </summary>
        /// <param name="validation">The failing validation.</param>
        /// <returns>The rejected result.</returns>
        public static BranchConfirmationResult Rejected(BranchValidation validation)
        {
            return new BranchConfirmationResult(false, validation, null);
        }
    }
}