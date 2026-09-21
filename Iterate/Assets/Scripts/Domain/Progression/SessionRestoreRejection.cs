namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Why a snapshot could not become a Session. Every member names a cross-holder rule no single
    /// holder can check for itself, which is why they are checked here rather than inside the holders.
    /// </summary>
    public enum SessionRestoreRejection
    {
        /// <summary>
        /// No rejection; the snapshot restored.
        /// </summary>
        None,

        /// <summary>
        /// The snapshot was written against a different catalog revision.
        /// </summary>
        CatalogRevisionMismatch,

        /// <summary>
        /// An instance identity is above the allocation counter, so the next allocation would collide with it.
        /// </summary>
        InstanceAboveCounter,

        /// <summary>
        /// Two positions carry the same instance identity.
        /// </summary>
        DuplicateInstance,

        /// <summary>
        /// A Repository entry carries an acquisition suffix below one.
        /// </summary>
        SuffixInvalid,

        /// <summary>
        /// The confirmed Branch names an instance the Repository does not hold.
        /// </summary>
        BranchMemberMissing,

        /// <summary>
        /// The starter Dependency consumes RAM, which a starter Dependency may not.
        /// </summary>
        StarterConsumesRAM,

        /// <summary>
        /// The installed Dependencies need more RAM than the capacity allows.
        /// </summary>
        RAMOverCapacity,

        /// <summary>
        /// One Utility is committed twice to the same Process.
        /// </summary>
        DuplicateUtilityCommitment,

        /// <summary>
        /// An occurrence-ordinal counter is malformed or repeats a pair.
        /// </summary>
        OrdinalCounterInvalid,

        /// <summary>
        /// A transaction counter is below one or repeats a kind.
        /// </summary>
        TransactionCounterInvalid
    }
}