namespace Iterate.Application.Content.Json
{
    /// <summary>
    /// How reading a versioned document ended.
    /// </summary>
    public enum VersionedDocumentOutcome
    {
        /// <summary>
        /// The document parsed and carries the expected format version.
        /// </summary>
        Loaded,

        /// <summary>
        /// The text is not a JSON object carrying an integer format version.
        /// </summary>
        Malformed,

        /// <summary>
        /// The document carries a format version this build does not read.
        /// </summary>
        UnsupportedVersion
    }
}