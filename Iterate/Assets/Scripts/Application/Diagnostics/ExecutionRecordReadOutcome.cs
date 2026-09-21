namespace Iterate.Application.Diagnostics
{
    /// <summary>
    /// How reading a generated execution record ended. Four outcomes stay distinguishable because a
    /// baseline comparison must never report a divergence for a file it simply could not read.
    /// </summary>
    public enum ExecutionRecordReadOutcome
    {
        /// <summary>
        /// The document parsed and its record was rebuilt.
        /// </summary>
        Loaded,

        /// <summary>
        /// The text is not a versioned document at all.
        /// </summary>
        Malformed,

        /// <summary>
        /// The document was written at a format version this build does not read.
        /// </summary>
        UnsupportedVersion,

        /// <summary>
        /// The document is well formed at a readable version, but its record could not be rebuilt.
        /// </summary>
        Defective
    }
}