using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// Validates the Structures file: the common definition fields, the source footprint, and the
    /// Repeat/Condition field-pairing shape with its predicate.
    /// </summary>
    public sealed class StructureRuleSet : ICategoryRuleSet
    {
        private static readonly ControlledVocabulary _allowedKeys = new(
            "id", "rulesText", "displayName", "category", "rarity", "tags",
            "sourceFootprint", "structureKind", "repeatCount", "predicate"
        );

        /// <summary>
        /// The file kind this rule set validates.
        /// </summary>
        public CatalogFileKind Kind => CatalogFileKind.Structure;

        /// <summary>
        /// The manifest category token this rule set answers to.
        /// </summary>
        public string CategoryToken => "STRUCTURE";

        /// <summary>
        /// Validates the Structures file's parsed value tree.
        /// </summary>
        /// <param name="fileRoot">The parsed file value tree.</param>
        /// <param name="context">The shared validation context.</param>
        public void ValidateFile(JsonValue fileRoot, CatalogValidationContext context)
        {
            if (fileRoot is not JsonArray array)
            {
                context.AddError("$", "definition.file-not-array", "a Structures file must be an array.");
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

                context.ValidateCommonFields(definition, path, CategoryToken, "STR", _allowedKeys);
                context.TryInteger(definition, "sourceFootprint", path, "definition.missing-field", "definition.field-type", out _);
                ValidatePairing(definition, path, context);
            }
        }

        private static void ValidatePairing(JsonObject definition, string path, CatalogValidationContext context)
        {
            if (!context.TryString(definition, "structureKind", path, "definition.missing-field", "definition.field-type", out string kind))
                return;

            bool hasPredicate = definition.TryGet("predicate", out JsonValue predicateNode);
            bool hasRepeat = definition.TryGet("repeatCount", out JsonValue repeatNode);

            if (!CatalogValidationContext.StructureKindTokens.Contains(kind))
            {
                context.AddError(path + ".structureKind", "definition.field-type", "'" + kind + "' is not a structure kind.");
                return;
            }

            if (kind == "REPEAT")
            {
                if (hasPredicate)
                    context.AddError(path, "definition.structure-pairing", "a Repeat Structure must not carry a predicate.");

                bool countOk = hasRepeat && repeatNode is JsonNumber number && number.IsInteger && number.IntegerValue >= 1L;
                if (!countOk)
                    context.AddError(path, "definition.structure-pairing", "a Repeat Structure requires a repeatCount of at least one.");

                return;
            }

            if (!hasPredicate)
                context.AddError(path, "definition.structure-pairing", "a Condition Structure requires a predicate.");
            else if (predicateNode is JsonObject predicate)
                ValidatePredicate(predicate, path + ".predicate", context);

            if (hasRepeat && repeatNode is JsonNumber { IsInteger: true } repeat && repeat.IntegerValue != 0L)
                context.AddError(path, "definition.structure-pairing", "a Condition Structure must carry a repeatCount of zero.");
        }

        private static void ValidatePredicate(JsonObject predicate, string path, CatalogValidationContext context)
        {
            if (context.TryString(predicate, "register", path, "definition.missing-field", "definition.field-type", out string register) && !CatalogValidationContext.RegisterTokens.Contains(register))
                context.AddError(path + ".register", "definition.field-type", "'" + register + "' is not a Core register.");

            if (context.TryString(predicate, "comparison", path, "definition.missing-field", "definition.field-type", out string comparison) && !CatalogValidationContext.PredicateComparisonTokens.Contains(comparison))
                context.AddError(path + ".comparison", "definition.field-type", "'" + comparison + "' is not a predicate comparison.");

            context.TryInteger(predicate, "operand", path, "definition.missing-field", "definition.field-type", out _);
        }
    }
}