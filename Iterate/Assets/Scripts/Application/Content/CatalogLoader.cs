using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// Loads the catalog: reads the manifest and each listed file through the port, parses them,
    /// validates the complete set, and freezes it into a <see cref="ContentCatalog"/>. All awaits use
    /// <c>ConfigureAwait(false)</c> (engine-free library code). Any defect throws a
    /// <see cref="CatalogLoadException"/> carrying the complete, file-named error list.
    /// </summary>
    public sealed class CatalogLoader
    {
        /// <summary>
        /// The conventional manifest file name.
        /// </summary>
        public const string ManifestFileName = "manifest.json";

        private readonly CatalogJsonReader _reader;
        private readonly ICatalogValidator _validator;
        private readonly CatalogFreezer _freezer;
        private readonly ICatalogFileSource _source;

        public CatalogLoader(
            CatalogJsonReader reader,
            ICatalogValidator validator,
            CatalogFreezer freezer,
            ICatalogFileSource source
        )
        {
            _reader = reader;
            _validator = validator;
            _freezer = freezer;
            _source = source;
        }

        /// <summary>
        /// Loads and freezes the catalog.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The frozen catalog.</returns>
        public async Task<ContentCatalog> LoadAsync(CancellationToken cancellationToken)
        {
            string manifestText;
            try
            {
                manifestText = await _source.ReadManifestAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new CatalogLoadException(new[]
                {
                    new CatalogError(ManifestFileName, "$", "file.unreadable", exception.Message)
                });
            }

            JsonValue manifestValue;
            try
            {
                manifestValue = _reader.Parse(manifestText);
            }
            catch (CatalogJsonParseException exception)
            {
                throw new CatalogLoadException(new[]
                {
                    new CatalogError(ManifestFileName, Locate(exception), "file.unparseable", exception.Message)
                });
            }

            List<CatalogError> errors = new();
            Dictionary<string, JsonValue> contentFiles = new(StringComparer.Ordinal);
            IReadOnlyList<string> fileNames = ExtractFileNames(manifestValue);
            for (int index = 0; index < fileNames.Count; index++)
            {
                string fileName = fileNames[index];
                if (contentFiles.ContainsKey(fileName)) continue;

                string text;
                try
                {
                    text = await _source.ReadFileAsync(fileName, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    errors.Add(new CatalogError(fileName, "$", "file.unreadable", exception.Message));
                    continue;
                }

                try
                {
                    contentFiles[fileName] = _reader.Parse(text);
                }
                catch (CatalogJsonParseException exception)
                {
                    errors.Add(new CatalogError(fileName, Locate(exception), "file.unparseable", exception.Message));
                }
            }

            CatalogFileSet fileSet = new(manifestValue, contentFiles);
            errors.AddRange(_validator.Validate(fileSet));
            if (errors.Count > 0) throw new CatalogLoadException(errors);

            CatalogManifest manifest = BuildManifest((JsonObject)manifestValue);
            try
            {
                return _freezer.Freeze(manifest, fileSet);
            }
            catch (CatalogLoadException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new CatalogLoadException(new[]
                {
                    new CatalogError(ManifestFileName, "$", "freeze.unexpected", exception.Message)
                });
            }
        }

        /// <summary>
        /// Formats a parse exception's position as a JSON-path substitute for the error record.
        /// </summary>
        /// <param name="exception">The parse exception.</param>
        /// <returns>The formatted line and column.</returns>
        private static string Locate(CatalogJsonParseException exception)
        {
            return "line " + exception.Line + ", column " + exception.Column;
        }

        /// <summary>
        /// Leniently extracts the listed content-file names from a parsed manifest so they can be read
        /// before validation; malformed manifests yield the names they can and the validator reports the
        /// rest.
        /// </summary>
        /// <param name="manifest">The parsed manifest value.</param>
        /// <returns>The listed file names.</returns>
        private static IReadOnlyList<string> ExtractFileNames(JsonValue manifest)
        {
            List<string> names = new();
            if (manifest is JsonObject root
                && root.TryGet("files", out JsonValue filesNode)
                && filesNode is JsonArray files)
            {
                for (int index = 0; index < files.Items.Count; index++)
                {
                    if (files.Items[index] is JsonObject entry
                        && entry.TryGet("file", out JsonValue fileNode)
                        && fileNode is JsonString fileName
                    )
                    {
                        names.Add(fileName.Value);
                    }
                }
            }

            return names;
        }

        /// <summary>
        /// Projects the validated manifest into the typed <see cref="CatalogManifest"/> the freezer
        /// consumes.
        /// </summary>
        /// <param name="manifest">The validated manifest object.</param>
        /// <returns>The typed manifest.</returns>
        private static CatalogManifest BuildManifest(JsonObject manifest)
        {
            manifest.TryGet("revision", out JsonValue revisionNode);
            manifest.TryGet("schemaVersion", out JsonValue schemaNode);
            manifest.TryGet("files", out JsonValue filesNode);
            JsonArray files = (JsonArray)filesNode;

            List<CatalogManifestEntry> entries = new(files.Items.Count);
            for (int index = 0; index < files.Items.Count; index++)
            {
                JsonObject entry = (JsonObject)files.Items[index];
                entry.TryGet("file", out JsonValue fileNode);
                entry.TryGet("category", out JsonValue categoryNode);
                entries.Add(new CatalogManifestEntry(((JsonString)fileNode).Value, ParseFileKind(((JsonString)categoryNode).Value)));
            }

            return new CatalogManifest(((JsonString)revisionNode).Value, ((JsonString)schemaNode).Value, entries);
        }

        /// <summary>
        /// Parses a manifest category token into its <see cref="CatalogFileKind"/>.
        /// </summary>
        /// <param name="token">The category token.</param>
        /// <returns>The parsed file kind.</returns>
        private static CatalogFileKind ParseFileKind(string token)
        {
            Enum.TryParse(token.Replace("_", string.Empty), true, out CatalogFileKind kind);
            return kind;
        }
    }
}