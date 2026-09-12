using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// Validates the Systems file: a System declares at least one stage, and each stage carries
    /// exactly the reference its kind admits. A route-selection stage's expansion into shop, Branch
    /// configuration and Process belongs to Session Flow, not to this file.
    /// </summary>
    public sealed class SystemRuleSet : ICategoryRuleSet
    {
        private static readonly ControlledVocabulary _allowedKeys = new("id", "displayName", "stages");

        private static readonly ControlledVocabulary _allowedStageKeys = new(
            "kind", "process", "shop", "routes"
        );

        private static readonly ControlledVocabulary _stageKinds = new("PROCESS", "SHOP", "ROUTE_SELECTION");

        /// <summary>
        /// The file kind this rule set validates.
        /// </summary>
        public CatalogFileKind Kind => CatalogFileKind.System;

        /// <summary>
        /// The manifest category token this rule set answers to.
        /// </summary>
        public string CategoryToken => "SYSTEM";

        /// <summary>
        /// Validates the Systems file's parsed value tree.
        /// </summary>
        /// <param name="fileRoot">The parsed file value tree.</param>
        /// <param name="context">The shared validation context.</param>
        public void ValidateFile(JsonValue fileRoot, CatalogValidationContext context)
        {
            if (fileRoot is not JsonArray array)
            {
                context.AddError("$", "definition.file-not-array", "a Systems file must be an array.");
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

                PackageFieldRules.ValidateIdentity(context, definition, path, "SYS", _allowedKeys);
                ValidateStages(definition, path, context);
            }
        }

        /// <summary>
        /// Validates one System's ordered stage list.
        /// </summary>
        /// <param name="definition">The System object.</param>
        /// <param name="jsonPath">The System's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        private static void ValidateStages(JsonObject definition, string jsonPath, CatalogValidationContext context)
        {
            if (!context.TryArray(definition, "stages", jsonPath, "definition.missing-field", "definition.field-type", out JsonArray stages))
                return;

            if (stages.Items.Count == 0)
            {
                context.AddError(jsonPath + ".stages", "system.stages-empty", "a System must declare at least one stage.");
                return;
            }

            for (int index = 0; index < stages.Items.Count; index++)
            {
                string path = jsonPath + ".stages[" + index + "]";
                if (stages.Items[index] is not JsonObject stage)
                {
                    context.AddError(path, "system.stage-shape", "each System stage must be an object.");
                    continue;
                }

                ValidateStage(stage, path, context);
            }
        }

        /// <summary>
        /// Validates one stage's kind and the reference that kind admits.
        /// </summary>
        /// <param name="stage">The stage object.</param>
        /// <param name="jsonPath">The stage's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        private static void ValidateStage(JsonObject stage, string jsonPath, CatalogValidationContext context)
        {
            context.RejectUnknownKeys(stage, _allowedStageKeys, jsonPath, "definition.unknown-field");

            if (!context.TryString(stage, "kind", jsonPath, "definition.missing-field", "definition.field-type", out string kind))
                return;

            if (!_stageKinds.Contains(kind))
            {
                context.AddError(jsonPath + ".kind", "system.unknown-stage-kind", "'" + kind + "' is not a System stage kind.");
                return;
            }

            bool hasProcess = stage.TryGet("process", out _);
            bool hasShop = stage.TryGet("shop", out _);
            bool hasRoutes = stage.TryGet("routes", out JsonValue routesValue);
            JsonArray routes = routesValue as JsonArray;
            int routeCount = routes == null ? 0 : routes.Items.Count;

            if (kind == "PROCESS")
            {
                if (!hasProcess || hasShop || hasRoutes)
                {
                    context.AddError(jsonPath, "system.stage-shape", "a Process stage carries a Process reference and nothing else.");
                    return;
                }

                PackageFieldRules.ValidateIDField(context, stage, "process", jsonPath, "WB-PROC-", "reference.wrong-kind");
                return;
            }

            if (kind == "SHOP")
            {
                if (!hasShop || hasProcess || hasRoutes)
                {
                    context.AddError(jsonPath, "system.stage-shape", "a shop stage carries a shop reference and nothing else.");
                    return;
                }

                PackageFieldRules.ValidateIDField(context, stage, "shop", jsonPath, "WB-SHOP-", "reference.wrong-kind");
                return;
            }

            if (routeCount == 0 || hasProcess || hasShop)
            {
                context.AddError(jsonPath, "system.stage-shape", "a route-selection stage carries at least one route and nothing else.");
                return;
            }

            for (int index = 0; index < routeCount; index++)
            {
                string path = jsonPath + ".routes[" + index + "]";
                if (routes.Items[index] is JsonString id)
                    context.RegisterIDReference(path, id.Value, "WB-ROUTE-", "reference.wrong-kind");
                else
                    context.AddError(path, "definition.field-type", "each route reference must be a string.");
            }
        }
    }
}