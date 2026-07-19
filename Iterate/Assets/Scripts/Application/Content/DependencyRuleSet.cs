using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// Validates the Dependencies file: the common definition fields, the RAM cost, and the required
    /// effects.
    /// </summary>
    public sealed class DependencyRuleSet : ICategoryRuleSet
    {
        private static readonly ControlledVocabulary _allowedKeys = new(
            "id", "rulesText", "displayName", "category", "rarity", "tags", "ram", "effects"
        );

        /// <summary>
        /// The file kind this rule set validates.
        /// </summary>
        public CatalogFileKind Kind => CatalogFileKind.Dependency;

        /// <summary>
        /// The manifest category token this rule set answers to.
        /// </summary>
        public string CategoryToken => "DEPENDENCY";

        /// <summary>
        /// Validates the Dependencies file's parsed value tree.
        /// </summary>
        /// <param name="fileRoot">The parsed file value tree.</param>
        /// <param name="context">The shared validation context.</param>
        public void ValidateFile(JsonValue fileRoot, CatalogValidationContext context)
        {
            if (fileRoot is not JsonArray array)
            {
                context.AddError("$", "definition.file-not-array", "a Dependencies file must be an array.");
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

                context.ValidateCommonFields(definition, path, CategoryToken, "DEP", _allowedKeys);
                context.TryInteger(definition, "ram", path, "definition.missing-field", "definition.field-type", out _);
                context.ValidateEffects(definition, path);
            }
        }
    }
}