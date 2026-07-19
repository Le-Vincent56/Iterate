using System;
using System.Collections.Generic;
using Iterate.Application.Content.Json;

namespace Iterate.Application.Content
{
    /// <summary>
    /// The cross-cutting catalog validator. Interprets the manifest, dispatches each
    /// listed file to its <see cref="ICategoryRuleSet"/>, and owns the catalog-wide rules (listed vs.
    /// present files, duplicate IDs across the catalog, and cross-reference resolution). Returns the
    /// complete error list, empty when the catalog is valid.
    /// </summary>
    public sealed class CatalogValidator : ICatalogValidator
    {
        /// <summary>
        /// The conventional manifest file name stamped on manifest-level errors.
        /// </summary>
        public const string ManifestFile = "manifest.json";

        private readonly IReadOnlyDictionary<string, ICategoryRuleSet> _byToken;

        public CatalogValidator()
        {
            ICategoryRuleSet[] ruleSets =
            {
                new ParameterRuleSet(),
                new InstructionRuleSet(),
                new StructureRuleSet(),
                new DirectiveRuleSet(),
                new DependencyRuleSet(),
                new PatchRuleSet(),
                new UtilityRuleSet()
            };

            Dictionary<string, ICategoryRuleSet> byToken = new(StringComparer.Ordinal);
            foreach (ICategoryRuleSet ruleSet in ruleSets)
            {
                byToken[ruleSet.CategoryToken] = ruleSet;
            }

            _byToken = byToken;
        }

        /// <summary>
        /// Validates a parsed catalog file set, returning the complete error list.
        /// </summary>
        /// <param name="files">The parsed manifest and content files.</param>
        /// <returns>The complete error list, empty when valid.</returns>
        public IReadOnlyList<CatalogError> Validate(CatalogFileSet files)
        {
            CatalogValidationContext context = new();
            context.CurrentFile = ManifestFile;

            HashSet<string> listed = new(StringComparer.Ordinal);
            List<PlannedFile> plan = new();
            InterpretManifest(files, context, listed, plan);

            foreach (KeyValuePair<string, JsonValue> entry in files.ContentFiles)
            {
                if (!listed.Contains(entry.Key))
                    context.AddError(entry.Key, "manifest.unlisted-file", "the file '" + entry.Key + "' is present but not listed in the manifest.");
            }

            for (int index = 0; index < plan.Count; index++)
            {
                PlannedFile planned = plan[index];
                context.CurrentFile = planned.File;
                planned.RuleSet.ValidateFile(files.ContentFiles[planned.File], context);
            }

            context.CurrentFile = ManifestFile;
            context.ResolveReferences();
            return context.Errors;
        }

        private void InterpretManifest(
            CatalogFileSet files,
            CatalogValidationContext context,
            HashSet<string> listed,
            List<PlannedFile> plan
        )
        {
            if (!(files.Manifest is JsonObject manifest))
            {
                context.AddError("$", "manifest.not-object", "the manifest root must be an object.");
                return;
            }

            if (context.TryString(manifest, "revision", "$", "manifest.missing-field", "manifest.field-type", out string revision)&& revision.Length == 0)
                context.AddError("$.revision", "manifest.revision-empty", "the revision must be non-empty.");

            context.TryString(manifest, "schemaVersion", "$", "manifest.missing-field", "manifest.field-type", out _);

            if (!context.TryArray(manifest, "files", "$", "manifest.missing-field", "manifest.field-type", out JsonArray fileEntries))
                return;

            for (int index = 0; index < fileEntries.Items.Count; index++)
            {
                string path = "$.files[" + index + "]";
                if (!(fileEntries.Items[index] is JsonObject entry))
                {
                    context.AddError(path, "manifest.entry-invalid", "each manifest entry must be an object.");
                    continue;
                }

                bool hasFile = context.TryString(entry, "file", path, "manifest.entry-invalid", "manifest.entry-invalid", out string file);
                bool hasCategory = context.TryString(entry, "category", path, "manifest.entry-invalid", "manifest.entry-invalid", out string category);
                if (!hasFile || !hasCategory) continue;

                if (!listed.Add(file))
                {
                    context.AddError(path, "manifest.duplicate-file", "the file '" + file + "' is listed more than once.");
                    continue;
                }

                if (!_byToken.TryGetValue(category, out ICategoryRuleSet ruleSet))
                {
                    context.AddError(path, "manifest.unknown-category", "'" + category + "' is not a known category.");
                    continue;
                }

                if (!files.ContentFiles.ContainsKey(file))
                {
                    context.AddError(path, "manifest.file-missing", "the listed file '" + file + "' is absent from the file set.");
                    continue;
                }

                plan.Add(new PlannedFile(file, ruleSet));
            }
        }

        /// <summary>
        /// A file scheduled for validation and the rule set that will validate it.
        /// </summary>
        /// <param name="File">The content file name.</param>
        /// <param name="RuleSet">The rule set for the file's kind.</param>
        private readonly record struct PlannedFile(string File, ICategoryRuleSet RuleSet);
    }
}