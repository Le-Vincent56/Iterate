namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Why a Session could not begin.
    /// </summary>
    public enum SessionBeginRejection
    {
        /// <summary>
        /// The Session began.
        /// </summary>
        None,

        /// <summary>
        /// A Session is already in progress.
        /// </summary>
        SessionAlreadyActive,

        /// <summary>
        /// The catalog has not loaded.
        /// </summary>
        CatalogUnavailable,

        /// <summary>
        /// The catalog defines no such System.
        /// </summary>
        UnknownSystem,

        /// <summary>
        /// The System holds no step for the Process a restored Session resumes at.
        /// </summary>
        UnknownProcess,

        /// <summary>
        /// The System offers no such route.
        /// </summary>
        UnknownRoute,
    }
}