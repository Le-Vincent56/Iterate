namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Where a Session stands. A stage decides which commands are legal; it never decides how a
    /// command applies. <see cref="BranchConfiguration"/> is derived from a Process's configuration,
    /// never authored, and there is no execution stage, because execution completes inside one
    /// command.
    /// </summary>
    public enum SessionStage
    {
        /// <summary>
        /// No Session has begun.
        /// </summary>
        NoSession,

        /// <summary>
        /// The player chooses one of the authored routes.
        /// </summary>
        RouteSelection,

        /// <summary>
        /// A shop is open.
        /// </summary>
        Shop,

        /// <summary>
        /// The player configures the Active Branch for the next Process.
        /// </summary>
        BranchConfiguration,

        /// <summary>
        /// The player edits source inside the Core.
        /// </summary>
        Build,

        /// <summary>
        /// The player reviews an execution's result and chooses how to leave it.
        /// </summary>
        ResultReview,

        /// <summary>
        /// The player resolves a submitted Process's rewards; the stage blocks until every
        /// component is resolved.
        /// </summary>
        Rewards,

        /// <summary>
        /// The Session is over; see its end reason.
        /// </summary>
        SessionEnded,
    }
}