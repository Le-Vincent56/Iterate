namespace Iterate.Domain.Trace
{
    /// <summary>
    /// How a runtime unit came to execute: as part of the Core's canonical top-to-bottom traversal, or
    /// as an added execution scheduled by a request. Rescue and Repeat occurrences remain the same unit
    /// or arrive with their children, so they need no separate activation kind.
    /// </summary>
    public enum ActivationKind
    {
        /// <summary>
        /// The unit executes as part of the canonical traversal.
        /// </summary>
        CanonicalTraversal,

        /// <summary>
        /// The unit executes as an added execution scheduled by a request.
        /// </summary>
        AddedExecution
    }
}