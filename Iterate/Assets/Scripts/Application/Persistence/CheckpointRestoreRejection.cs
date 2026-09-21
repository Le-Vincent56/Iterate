namespace Iterate.Application.Persistence
{
    /// <summary>
    /// Why a checkpoint could not become a Session. A checkpoint the build cannot honour is refused
    /// whole: there is no partial restore for a caller to mistake for a complete one.
    /// </summary>
    public enum CheckpointRestoreRejection
    {
        /// <summary>
        /// No rejection; the checkpoint restored.
        /// </summary>
        None,

        /// <summary>
        /// The checkpoint was written against a different catalog revision.
        /// </summary>
        CatalogRevisionMismatch,

        /// <summary>
        /// The checkpoint was written against a different deterministic random service revision.
        /// </summary>
        RandomServiceRevisionMismatch,

        /// <summary>
        /// The catalog defines no Process configuration for the checkpoint's Process.
        /// </summary>
        UnknownProcess,

        /// <summary>
        /// The Process declares a role this build has no recovery for.
        /// </summary>
        UnknownProcessRole,

        /// <summary>
        /// The catalog defines no route for the checkpoint's route.
        /// </summary>
        UnknownRoute,

        /// <summary>
        /// The checkpoint names content the catalog does not define at all.
        /// </summary>
        UnknownDefinition,

        /// <summary>
        /// The checkpoint names content the catalog defines as another kind than the position requires.
        /// </summary>
        DefinitionCategoryMismatch,

        /// <summary>
        /// The content resolved, but the Session it describes breaks one of its own cross-holder rules.
        /// </summary>
        SessionRejected
    }
}