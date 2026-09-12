using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// Validates the Cores file: one-based contiguous line positions, exactly the payload each line
    /// kind admits, and a designated final output naming a fixed-instruction line.
    /// </summary>
    public sealed class CoreRuleSet : ICategoryRuleSet
    {
        private static readonly ControlledVocabulary _allowedKeys = new(
            "id", "displayName", "lines", "finalOutputPosition"
        );

        private static readonly ControlledVocabulary _allowedLineKeys = new(
            "position", "kind", "operation", "predicate", "contained"
        );

        private static readonly ControlledVocabulary _lineKinds = new(
            "FIXED_INSTRUCTION", "OPEN", "FIXED_STRUCTURE"
        );

        private static readonly ControlledVocabulary _lineOperators = new("ASSIGN", "ADD", "MULTIPLY");

        /// <summary>
        /// The file kind this rule set validates.
        /// </summary>
        public CatalogFileKind Kind => CatalogFileKind.Core;

        /// <summary>
        /// The manifest category token this rule set answers to.
        /// </summary>
        public string CategoryToken => "CORE";

        /// <summary>
        /// Validates the Cores file's parsed value tree.
        /// </summary>
        /// <param name="fileRoot">The parsed file value tree.</param>
        /// <param name="context">The shared validation context.</param>
        public void ValidateFile(JsonValue fileRoot, CatalogValidationContext context)
        {
            if (fileRoot is not JsonArray array)
            {
                context.AddError("$", "definition.file-not-array", "a Cores file must be an array.");
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

                PackageFieldRules.ValidateIdentity(context, definition, path, "CORE", _allowedKeys);
                ValidateCore(definition, path, context);
            }
        }

        /// <summary>
        /// Validates one Core's line composition and designated final output.
        /// </summary>
        /// <param name="definition">The Core object.</param>
        /// <param name="jsonPath">The Core's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        private static void ValidateCore(JsonObject definition, string jsonPath, CatalogValidationContext context)
        {
            if (!context.TryArray(definition, "lines", jsonPath, "definition.missing-field", "definition.field-type", out JsonArray lines))
                return;

            for (int index = 0; index < lines.Items.Count; index++)
            {
                string path = jsonPath + ".lines[" + index + "]";
                if (lines.Items[index] is not JsonObject line)
                {
                    context.AddError(path, "core.line-shape", "each Core line must be an object.");
                    continue;
                }

                if (context.TryInteger(line, "position", path, "definition.missing-field", "definition.field-type", out long position) && position != index + 1)
                    context.AddError(path + ".position", "core.positions-not-contiguous", "Core line positions must run from one without gaps.");

                ValidateLine(line, path, context, false);
            }

            if (!context.TryInteger(definition, "finalOutputPosition", jsonPath, "definition.missing-field", "definition.field-type", out long finalOutput))
                return;

            string outputPath = jsonPath + ".finalOutputPosition";
            if (finalOutput < 1 || finalOutput > lines.Items.Count)
            {
                context.AddError(outputPath, "core.final-output-not-fixed-instruction", "the designated final output must name one of the Core's own lines.");
                return;
            }

            if (lines.Items[(int)finalOutput - 1] is not JsonObject outputLine)
                return;

            if (context.TryString(outputLine, "kind", outputPath, "definition.missing-field", "definition.field-type", out string outputKind) && outputKind != "FIXED_INSTRUCTION")
                context.AddError(outputPath, "core.final-output-not-fixed-instruction", "the designated final output must name a fixed-instruction line.");
        }

        /// <summary>
        /// Validates one Core line's kind and the payload that kind admits.
        /// </summary>
        /// <param name="line">The line object.</param>
        /// <param name="jsonPath">The line's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        /// <param name="isContained">Whether the line is a fixed Structure's contained line.</param>
        private static void ValidateLine(
            JsonObject line,
            string jsonPath,
            CatalogValidationContext context,
            bool isContained
        )
        {
            context.RejectUnknownKeys(line, _allowedLineKeys, jsonPath, "definition.unknown-field");

            if (!context.TryString(line, "kind", jsonPath, "definition.missing-field", "definition.field-type", out string kind))
                return;

            if (!_lineKinds.Contains(kind))
            {
                context.AddError(jsonPath + ".kind", "core.unknown-line-kind", "'" + kind + "' is not a Core line kind.");
                return;
            }

            bool hasOperation = line.TryGet("operation", out JsonValue operationValue);
            bool hasPredicate = line.TryGet("predicate", out JsonValue predicateValue);
            bool hasContained = line.TryGet("contained", out JsonValue containedValue);

            switch (kind)
            {
                case "FIXED_INSTRUCTION":
                {
                    if (!hasOperation)
                        context.AddError(jsonPath, "core.line-shape", "a fixed-instruction Core line requires an operation.");

                    if (hasPredicate || hasContained)
                        context.AddError(jsonPath, "core.line-shape", "a fixed-instruction Core line carries no predicate or contained line.");

                    if (hasOperation && operationValue is JsonObject operation)
                        ValidateOperation(operation, jsonPath + ".operation", context);

                    return;
                }
                
                case "OPEN":
                {
                    if (hasOperation || hasPredicate || hasContained)
                        context.AddError(jsonPath, "core.line-shape", "an open Core line carries no payload.");

                    return;
                }
            }

            if (isContained)
            {
                context.AddError(jsonPath, "core.line-shape", "a fixed Structure contains a fixed instruction, not another Structure.");
                return;
            }

            if (hasOperation)
                context.AddError(jsonPath, "core.line-shape", "a fixed-Structure Core line carries no operation.");

            if (!hasPredicate)
                context.AddError(jsonPath, "core.line-shape", "a fixed-Structure Core line requires a predicate.");

            if (!hasContained)
            {
                context.AddError(jsonPath, "core.line-shape", "a fixed-Structure Core line requires a contained line.");
                return;
            }

            if (hasPredicate && predicateValue is JsonObject predicate)
                ValidatePredicate(predicate, jsonPath + ".predicate", context);

            if (containedValue is JsonObject contained)
                ValidateLine(contained, jsonPath + ".contained", context, true);
            else
                context.AddError(jsonPath + ".contained", "core.line-shape", "a contained Core line must be an object.");
        }

        /// <summary>
        /// Validates a Core line's typed operation payload.
        /// </summary>
        /// <param name="operation">The operation object.</param>
        /// <param name="jsonPath">The operation's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        private static void ValidateOperation(JsonObject operation, string jsonPath, CatalogValidationContext context)
        {
            if (context.TryString(operation, "operator", jsonPath, "definition.missing-field", "definition.field-type", out string lineOperator))
                context.RequireMembership(lineOperator, _lineOperators, jsonPath + ".operator", "core.unknown-operator");

            if (context.TryString(operation, "register", jsonPath, "definition.missing-field", "definition.field-type", out string register))
                context.RequireMembership(register, CatalogValidationContext.RegisterTokens, jsonPath + ".register", "core.unknown-register");

            if (!context.TryObject(operation, "operand", jsonPath, "definition.missing-field", "definition.field-type", out JsonObject operand))
                return;

            if (context.TryString(operand, "source", jsonPath + ".operand", "definition.missing-field", "definition.field-type", out string source))
                context.RequireMembership(source, CatalogValidationContext.OperandSourceTokens, jsonPath + ".operand.source", "core.unknown-operand-source");
        }

        /// <summary>
        /// Validates a fixed Structure's predicate payload.
        /// </summary>
        /// <param name="predicate">The predicate object.</param>
        /// <param name="jsonPath">The predicate's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        private static void ValidatePredicate(JsonObject predicate, string jsonPath, CatalogValidationContext context)
        {
            if (context.TryString(predicate, "register", jsonPath, "definition.missing-field", "definition.field-type", out string register))
                context.RequireMembership(register, CatalogValidationContext.RegisterTokens, jsonPath + ".register", "core.unknown-register");

            if (context.TryString(predicate, "comparison", jsonPath, "definition.missing-field", "definition.field-type", out string comparison))
                context.RequireMembership(comparison, CatalogValidationContext.PredicateComparisonTokens, jsonPath + ".comparison", "core.unknown-comparison");

            context.TryInteger(predicate, "operand", jsonPath, "definition.missing-field", "definition.field-type", out _);
        }
    }
}