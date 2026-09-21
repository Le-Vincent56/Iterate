using Iterate.Application.Content.Json;
using Iterate.Application.Persistence;
using Iterate.Domain.Trace;

namespace Iterate.Application.Diagnostics
{
    /// <summary>
    /// Writes and reads a generated execution record inside the versioned envelope. The record codec
    /// carries its own format version rather than the save files': a diagnostic artifact and a save file
    /// change shape for different reasons and must be able to move independently.
    ///
    /// A record written, read and written again produces the same bytes. That is not a nicety — it is
    /// what lets a committed baseline be compared by diff, so that a refresh which changes nothing shows
    /// nothing.
    /// </summary>
    public static class ExecutionRecordDocument
    {
        /// <summary>
        /// Writes a record as a document.
        /// </summary>
        /// <param name="record">The record to write.</param>
        /// <returns>The document text.</returns>
        public static string Write(ExecutionRecord record)
        {
            return VersionedDocument.Write(ExecutionRecordCodec.FormatVersion, ExecutionRecordCodec.ToJson(record));
        }

        /// <summary>
        /// Reads a document back into a record.
        /// </summary>
        /// <param name="text">The document text.</param>
        /// <returns>The read result.</returns>
        public static ExecutionRecordReadResult Read(string text)
        {
            VersionedDocumentReadResult document = VersionedDocument.Read(text, ExecutionRecordCodec.FormatVersion);
            if (document.Outcome == VersionedDocumentOutcome.Malformed)
                return new ExecutionRecordReadResult(ExecutionRecordReadOutcome.Malformed, null, 0, document.Reason);

            if (document.Outcome == VersionedDocumentOutcome.UnsupportedVersion)
            {
                return new ExecutionRecordReadResult(
                    ExecutionRecordReadOutcome.UnsupportedVersion,
                    null,
                    document.FormatVersion,
                    document.Reason
                );
            }

            CodecResult<ExecutionRecord> decoded = ExecutionRecordCodec.FromJson(document.Payload);
            if (!decoded.Succeeded)
            {
                return new ExecutionRecordReadResult(
                    ExecutionRecordReadOutcome.Defective,
                    null,
                    document.FormatVersion,
                    decoded.Defect
                );
            }

            return new ExecutionRecordReadResult(
                ExecutionRecordReadOutcome.Loaded,
                decoded.Value,
                document.FormatVersion,
                null
            );
        }
    }
}