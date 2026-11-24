namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// What a planned step asks the Session to do next.
    /// </summary>
    public enum PlannedStepKind
    {
        /// <summary>
        /// Enter a Process, through Branch configuration when the Process declares a Branch.
        /// </summary>
        Process,

        /// <summary>
        /// Open a shop.
        /// </summary>
        Shop,

        /// <summary>
        /// Offer a choice between routes.
        /// </summary>
        RouteSelection,

        /// <summary>
        /// The System has no further step; the Session completes.
        /// </summary>
        End,
    }
}