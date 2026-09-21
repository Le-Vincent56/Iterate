namespace Iterate.Application.Content.Json
{
    /// <summary>
    /// The outcome of reading a versioned document. A payload is present only when the document loaded;
    /// the version is present when one could be read at all, so an unsupported document can report which
    /// version it was written at.
    /// </summary>
    /// <param name="Outcome">How the read ended.</param>
    /// <param name="Payload">The whole document object, or null.</param>
    /// <param name="FormatVersion">The version found, or zero when none could be read.</param>
    /// <param name="Reason">A sentence naming what was wrong, or null.</param>
    public sealed record VersionedDocumentReadResult(
        VersionedDocumentOutcome Outcome,
        JsonObject Payload,
        int FormatVersion,
        string Reason
    );
}