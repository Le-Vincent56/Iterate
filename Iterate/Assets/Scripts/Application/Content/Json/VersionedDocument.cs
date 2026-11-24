using System;
using System.Globalization;

namespace Iterate.Application.Content.Json
{
    /// <summary>
    /// The envelope every generated JSON document this project writes is wrapped in: one object whose
    /// first member is the format version, followed by the payload's own members in their own order.
    /// The version is stamped from the very first write, so a file written today can be recognised — or
    /// refused — by a build that reads a later format. Reading reports an outcome rather than throwing,
    /// because a file on disk is data the game found, not a contract a caller broke.
    ///
    /// It lives beside the reader rather than with the save codecs so that diagnostics and saves can
    /// both depend on it without depending on each other.
    /// </summary>
    public static class VersionedDocument
    {
        /// <summary>
        /// The member every versioned document carries first.
        /// </summary>
        public const string VersionMember = "formatVersion";

        /// <summary>
        /// Writes a payload inside the envelope.
        /// </summary>
        /// <param name="formatVersion">The format version to stamp.</param>
        /// <param name="payload">The payload members, in the order they should be written.</param>
        /// <returns>The document text.</returns>
        /// <exception cref="ArgumentException">Thrown when the payload is null or carries its own version member.</exception>
        public static string Write(int formatVersion, JsonObject payload)
        {
            if (payload == null)
                throw new ArgumentException("a versioned document requires a payload.", nameof(payload));

            JsonObjectBuilder builder = new();
            builder.Add(VersionMember, formatVersion);
            for (int index = 0; index < payload.Keys.Count; index++)
            {
                string key = payload.Keys[index];
                if (string.Equals(key, VersionMember, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        $"the payload carries its own '{VersionMember}' member.",
                        nameof(payload)
                    );
                }

                payload.TryGet(key, out JsonValue value);
                builder.Add(key, value);
            }

            return JsonWriter.Write(builder.Build());
        }

        /// <summary>
        /// Reads a versioned document, separating text that is not one at all from a document written at
        /// a version this build does not read.
        /// </summary>
        /// <param name="text">The document text.</param>
        /// <param name="expectedVersion">The format version this build reads.</param>
        /// <returns>The read result; the payload is the whole object, version member included.</returns>
        public static VersionedDocumentReadResult Read(string text, int expectedVersion)
        {
            if (string.IsNullOrEmpty(text))
                return Malformed("the document is empty.");

            JsonValue root;
            try
            {
                root = new CatalogJsonReader().Parse(text);
            }
            catch (CatalogJsonParseException parseFailure)
            {
                return Malformed(parseFailure.Message);
            }
            catch (OverflowException overflow)
            {
                return Malformed(overflow.Message);
            }

            if (root is not JsonObject document)
                return Malformed("the document's root is not an object.");

            if (!document.TryGet(VersionMember, out JsonValue version))
                return Malformed($"the document has no '{VersionMember}' member.");

            if (version is not JsonNumber { IsInteger: true } number
                || number.IntegerValue < int.MinValue
                || number.IntegerValue > int.MaxValue)
            {
                return Malformed($"the '{VersionMember}' member is not an integer.");
            }

            int found = (int)number.IntegerValue;
            if (found != expectedVersion)
            {
                return new VersionedDocumentReadResult(
                    VersionedDocumentOutcome.UnsupportedVersion,
                    null,
                    found,
                    $"the document was written at format version {found.ToString(CultureInfo.InvariantCulture)}."
                );
            }

            return new VersionedDocumentReadResult(VersionedDocumentOutcome.Loaded, document, found, null);
        }

        /// <summary>
        /// Builds a malformed result carrying a reason and nothing else.
        /// </summary>
        /// <param name="reason">What was wrong with the text.</param>
        /// <returns>The result.</returns>
        private static VersionedDocumentReadResult Malformed(string reason)
        {
            return new VersionedDocumentReadResult(VersionedDocumentOutcome.Malformed, null, 0, reason);
        }
    }
}