using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Domain.Content;
using Iterate.Infrastructure.Content;

namespace Iterate.Editor.Content
{
    /// <summary>
    /// The Iterate → Validate Catalog Editor menu item: runs the identical catalog load/validate
    /// pipeline the boot path uses over the project's StreamingAssets catalog folder, logs a pass/fail
    /// summary and every error, and flags stray unlisted JSON files. Editor tooling logs through
    /// UnityEngine.Debug directly (no composition root in Edit mode) — the sanctioned Editor-assembly
    /// exception to the runtime single-sink constraint.
    /// </summary>
    public static class CatalogValidatorMenu
    {
        private const string CatalogFolder = "Catalog";

        private const string ManifestFileName = "manifest.json";

        private const string SchemaFileName = "catalog.schema.json";

        /// <summary>
        /// Validates the shipped catalog and reports the outcome to the Console.
        /// </summary>
        [MenuItem("Iterate/Validate Catalog")]
        public static void ValidateCatalog()
        {
            string root = Path.Combine(UnityEngine.Application.streamingAssetsPath, CatalogFolder);
            if (!Directory.Exists(root))
            {
                Debug.LogError("[Catalog] Validate Catalog: the catalog folder was not found at " + root + ".");
                return;
            }

            CatalogLoader loader = new(
                new CatalogJsonReader(),
                new CatalogValidator(),
                new CatalogFreezer(),
                new CatalogDirectorySource(root)
            );

            try
            {
                ContentCatalog catalog = Task.Run(() => loader.LoadAsync(CancellationToken.None)).GetAwaiter().GetResult();
                Debug.Log("[Catalog] Validate Catalog: PASS | revision=" + catalog.Revision + " | definitions=" + catalog.DefinitionCount);
            }
            catch (CatalogLoadException exception)
            {
                Debug.LogError("[Catalog] Validate Catalog: FAIL | " + exception.Errors.Count + " error(s).");
                foreach (CatalogError error in exception.Errors)
                {
                    Debug.LogError("[Catalog]   " + error.File + " | " + error.JsonPath + " | " + error.RuleName + " | " + error.Message);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("[Catalog] Validate Catalog: FAIL | unexpected error: " + exception.Message);
            }

            FlagStrayFiles(root);
        }

        /// <summary>
        /// Flags any .json file in the folder that the manifest does not list (excluding the manifest
        /// and schema themselves) — strays the loader never reads and the validator therefore never
        /// sees.
        /// </summary>
        /// <param name="root">The catalog folder.</param>
        private static void FlagStrayFiles(string root)
        {
            HashSet<string> known = new(StringComparer.Ordinal) { ManifestFileName, SchemaFileName };
            foreach (string listed in ListedFiles(root))
            {
                known.Add(listed);
            }

            string[] present = Directory.GetFiles(root, "*.json");
            for (int index = 0; index < present.Length; index++)
            {
                string name = Path.GetFileName(present[index]);
                if (!known.Contains(name))
                    Debug.LogWarning("[Catalog]   stray unlisted file: " + name);
            }
        }

        /// <summary>
        /// Reads the manifest and extracts its listed content-file names; returns an empty list when the
        /// manifest is missing or unparseable (the load pass already reported that).
        /// </summary>
        /// <param name="root">The catalog folder.</param>
        /// <returns>The listed file names.</returns>
        private static IReadOnlyList<string> ListedFiles(string root)
        {
            List<string> names = new();
            string manifestPath = Path.Combine(root, ManifestFileName);
            if (!File.Exists(manifestPath))
                return names;

            JsonValue manifest;
            try
            {
                manifest = new CatalogJsonReader().Parse(File.ReadAllText(manifestPath));
            }
            catch (CatalogJsonParseException)
            {
                return names;
            }

            if (manifest is not JsonObject manifestObject
                || !manifestObject.TryGet("files", out JsonValue filesNode)
                || filesNode is not JsonArray files
            ) return names;
            
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

            return names;
        }
    }
}