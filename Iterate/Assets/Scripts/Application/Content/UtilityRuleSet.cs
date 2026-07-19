using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// Validates the Utilities file: the common definition fields and the required Process-setup
    /// effects.
    /// </summary>
    public sealed class UtilityRuleSet : ICategoryRuleSet
    {
        private static readonly ControlledVocabulary _allowedKeys = new(
            "id", "rulesText", "displayName", "category", "rarity", "tags", "effects"
        );

        /// <summary>
        /// The file kind this rule set validates.
        /// </summary>
        public CatalogFileKind Kind => CatalogFileKind.Utility;

        /// <summary>
        /// The manifest category token this rule set answers to.
        /// </summary>
        public string CategoryToken => "UTILITY";

        /// <summary>
        /// Validates the Utilities file's parsed value tree.
        /// </summary>
        /// <param name="fileRoot">The parsed file value tree.</param>
        /// <param name="context">The shared validation context.</param>
        public void ValidateFile(JsonValue fileRoot, CatalogValidationContext context)
        {
            if (fileRoot is not JsonArray array)
            {
                context.AddError("$", "definition.file-not-array", "a Utilities file must be an array.");
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

                context.ValidateCommonFields(definition, path, CategoryToken, "UTL", _allowedKeys);
                context.ValidateEffects(definition, path);
            }
        }
    }
}