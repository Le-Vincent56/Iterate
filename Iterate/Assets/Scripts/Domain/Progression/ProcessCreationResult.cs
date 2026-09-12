namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The typed outcome of creating a Process: the opened Process, or the reason it could not open.
    /// </summary>
    /// <param name="Succeeded">Whether the Process was created.</param>
    /// <param name="Rejection">The rejection reason; None on success.</param>
    /// <param name="State">The created Process; null on rejection.</param>
    public readonly record struct ProcessCreationResult(
        bool Succeeded,
        ProcessCreationRejection Rejection,
        ProcessState State
    )
    {
        /// <summary>
        /// Creates a successful result carrying the Process.
        /// </summary>
        /// <param name="state">The created Process.</param>
        /// <returns>The creation result.</returns>
        public static ProcessCreationResult Success(ProcessState state)
        {
            return new ProcessCreationResult(true, ProcessCreationRejection.None, state);
        }

        /// <summary>
        /// Creates a rejected result carrying a reason.
        /// </summary>
        /// <param name="rejection">The rejection reason.</param>
        /// <returns>The creation result.</returns>
        public static ProcessCreationResult Rejected(ProcessCreationRejection rejection)
        {
            return new ProcessCreationResult(false, rejection, null);
        }
    }
}