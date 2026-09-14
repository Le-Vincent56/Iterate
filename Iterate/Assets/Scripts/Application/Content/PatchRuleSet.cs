using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// Validates the Patches file: the common definition fields, the host-eligibility rule, and the
    /// required effects. Host-eligibility is checked for membership of the controlled rule set, so a
    /// mistyped rule is a catalog error rather than a throw at attachment time.
    /// </summary>
    public sealed class PatchRuleSet : ICategoryRuleSet
    {
        private static readonly ControlledVocabulary _allowedKeys = new(
            "id", "rulesText", "displayName", "category", "rarity", "tags", "hostEligibility", "effects"
        );

        /// <summary>
        /// The file kind this rule set validates.
        /// </summary>
        public CatalogFileKind Kind => CatalogFileKind.Patch;

        /// <summary>
        /// The manifest category token this rule set answers to.
        /// </summary>
        public string CategoryToken => "PATCH";

        /// <summary>
        /// Validates the Patches file's parsed value tree.
        /// </summary>
        /// <param name="fileRoot">The parsed file value tree.</param>
        /// <param name="context">The shared validation context.</param>
        public void ValidateFile(JsonValue fileRoot, CatalogValidationContext context)
        {
            if (fileRoot is not JsonArray array)
            {
                context.AddError("$", "definition.file-not-array", "a Patches file must be an array.");
                return;
            }

            for (int index = 0; index < array.Items.Count; index++)
            {
                string path = "$[" + index + "]";
                if (array.Items[index] is not JsonObject definition)
                {
                    context.AddError(path, "definition.not-object", "each definition must be an object.");
                    continue;
                }

                context.ValidateCommonFields(definition, path, CategoryToken, "PAT", _allowedKeys);
                
                if (context.TryString(definition, "hostEligibility", path, "definition.missing-field", "definition.field-type", out string hostEligibility))
                {
                    context.RequireMembership(
                        hostEligibility,
                        CatalogVocabulary.PatchHostEligibilityRules,
                        path + ".hostEligibility",
                        "patch.unknown-host-eligibility"
                    );
                }
                
                context.ValidateEffects(definition, path);
            }
        }
    }
}