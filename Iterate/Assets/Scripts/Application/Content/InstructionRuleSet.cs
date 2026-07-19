using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// Validates the Instructions file: the common definition fields, the source footprint, the
    /// quantity-change primary operation, the optional position qualifier, and the optional
    /// ineligibility cross-references.
    /// </summary>
    public sealed class InstructionRuleSet : ICategoryRuleSet
    {
        private static readonly ControlledVocabulary _allowedKeys = new(
            "id", "rulesText", "displayName", "category", "rarity", "tags",
            "sourceFootprint", "primaryOperation", "positionQualifier", "ineligibilityTags"
        );

        /// <summary>
        /// The file kind this rule set validates.
        /// </summary>
        public CatalogFileKind Kind => CatalogFileKind.Instruction;

        /// <summary>
        /// The manifest category token this rule set answers to.
        /// </summary>
        public string CategoryToken => "INSTRUCTION";

        /// <summary>
        /// Validates the Instructions file's parsed value tree.
        /// </summary>
        /// <param name="fileRoot">The parsed file value tree.</param>
        /// <param name="context">The shared validation context.</param>
        public void ValidateFile(JsonValue fileRoot, CatalogValidationContext context)
        {
            if (!(fileRoot is JsonArray array))
            {
                context.AddError("$", "definition.file-not-array", "an Instructions file must be an array.");
                return;
            }

            for (int index = 0; index < array.Items.Count; index++)
            {
                string path = "$[" + index + "]";
                if (!(array.Items[index] is JsonObject definition))
                {
                    context.AddError(path, "definition.not-object", "each definition must be an object.");
                    continue;
                }

                context.ValidateCommonFields(definition, path, CategoryToken, "INS", _allowedKeys);
                context.TryInteger(definition, "sourceFootprint", path, "definition.missing-field", "definition.field-type", out _);

                if (context.TryObject(definition, "primaryOperation", path, "definition.missing-field", "definition.field-type", out JsonObject primaryOperation))
                    context.ValidateOperation(primaryOperation, path + ".primaryOperation");

                if (definition.TryGet("positionQualifier", out JsonValue qualifierNode) && qualifierNode is JsonObject qualifier)
                    context.ValidateQualifier(qualifier, path + ".positionQualifier");

                if (!definition.TryGet("ineligibilityTags", out JsonValue tagsNode) ||
                    tagsNode is not JsonArray tags) continue;
                
                for (int tagIndex = 0; tagIndex < tags.Items.Count; tagIndex++)
                {
                    if (tags.Items[tagIndex] is JsonString reference)
                        context.RegisterReference(path + ".ineligibilityTags[" + tagIndex + "]", reference.Value, "crossref.unresolved");
                }
            }
        }
    }
}