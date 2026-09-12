using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// Validates the Process-configurations file: strictly ascending thresholds, at least one
    /// execution, non-empty scripted arrival moments, a drawn load that fits the Active Branch, and
    /// exposure only on a drawn load. A scripted moment is deliberately not bounded by Buffer
    /// capacity — overflow is a legal authored state.
    /// </summary>
    public sealed class ProcessConfigurationRuleSet : ICategoryRuleSet
    {
        private static readonly ControlledVocabulary _allowedKeys = new(
            "id", "displayName", "role", "core", "processRule", "thresholds", "executions",
            "mandatoryExecutions", "startingBytes", "bufferCapacity", "sourceCapacity",
            "initialSource", "bufferLoad", "exposure", "activeBranch", "rewardPackage", "precedingShop"
        );

        private static readonly ControlledVocabulary _roles = new(
            "TUTORIAL_1", "TUTORIAL_2", "ROUTE_PROCESS_3", "CRITICAL_PROCESS"
        );

        private static readonly ControlledVocabulary _loadPolicies = new("SCRIPTED", "DRAWN");

        /// <summary>
        /// The file kind this rule set validates.
        /// </summary>
        public CatalogFileKind Kind => CatalogFileKind.ProcessConfiguration;

        /// <summary>
        /// The manifest category token this rule set answers to.
        /// </summary>
        public string CategoryToken => "PROCESS_CONFIGURATION";

        /// <summary>
        /// Validates the Process-configurations file's parsed value tree.
        /// </summary>
        /// <param name="fileRoot">The parsed file value tree.</param>
        /// <param name="context">The shared validation context.</param>
        public void ValidateFile(JsonValue fileRoot, CatalogValidationContext context)
        {
            if (fileRoot is not JsonArray array)
            {
                context.AddError("$", "definition.file-not-array", "a Process-configurations file must be an array.");
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

                PackageFieldRules.ValidateIdentity(context, definition, path, "PROC", _allowedKeys);
                ValidateConfiguration(definition, path, context);
            }
        }

        /// <summary>
        /// Validates one Process configuration's references, counters and load plan.
        /// </summary>
        /// <param name="definition">The configuration object.</param>
        /// <param name="jsonPath">The configuration's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        private static void ValidateConfiguration(JsonObject definition, string jsonPath, CatalogValidationContext context)
        {
            if (context.TryString(definition, "role", jsonPath, "definition.missing-field", "definition.field-type", out string role))
                context.RequireMembership(role, _roles, jsonPath + ".role", "process.unknown-role");

            PackageFieldRules.ValidateIDField(context, definition, "core", jsonPath, "WB-CORE-", "reference.wrong-kind");
            PackageFieldRules.ValidateIDField(context, definition, "rewardPackage", jsonPath, "WB-RWD-", "reference.wrong-kind");
            PackageFieldRules.ValidateOptionalIDField(context, definition, "processRule", jsonPath, "WB-PRC-", "reference.wrong-kind");
            PackageFieldRules.ValidateOptionalIDField(context, definition, "precedingShop", jsonPath, "WB-SHOP-", "reference.wrong-kind");

            ValidateThresholds(definition, jsonPath, context);

            if (context.TryInteger(definition, "executions", jsonPath, "definition.missing-field", "definition.field-type", out long executions) && executions < 1)
                context.AddError(jsonPath + ".executions", "process.executions-below-one", "a Process must allow at least one execution.");

            context.TryBoolean(definition, "mandatoryExecutions", jsonPath, "definition.missing-field", "definition.field-type", out _);
            context.TryInteger(definition, "startingBytes", jsonPath, "definition.missing-field", "definition.field-type", out _);
            context.TryInteger(definition, "bufferCapacity", jsonPath, "definition.missing-field", "definition.field-type", out _);
            context.TryInteger(definition, "sourceCapacity", jsonPath, "definition.missing-field", "definition.field-type", out _);

            ValidateInitialSource(definition, jsonPath, context);

            long branchCapacity = ValidateActiveBranch(definition, jsonPath, context);
            string policy = ValidateBufferLoad(definition, jsonPath, context, branchCapacity);
            ValidateExposure(definition, jsonPath, context, policy);
        }

        /// <summary>
        /// Validates the three authored thresholds and their strict ascent.
        /// </summary>
        /// <param name="definition">The configuration object.</param>
        /// <param name="jsonPath">The configuration's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        private static void ValidateThresholds(JsonObject definition, string jsonPath, CatalogValidationContext context)
        {
            if (!context.TryObject(definition, "thresholds", jsonPath, "definition.missing-field", "definition.field-type", out JsonObject thresholds))
                return;

            string path = jsonPath + ".thresholds";
            bool hasPass = context.TryInteger(thresholds, "pass", path, "definition.missing-field", "definition.field-type", out long pass);
            bool hasOptimize = context.TryInteger(thresholds, "optimize", path, "definition.missing-field", "definition.field-type", out long optimize);
            bool hasBenchmark = context.TryInteger(thresholds, "benchmark", path, "definition.missing-field", "definition.field-type", out long benchmark);
            if (!hasPass || !hasOptimize || !hasBenchmark)
                return;

            if (optimize <= pass || benchmark <= optimize)
                context.AddError(path, "process.thresholds-not-ascending", "Pass, Optimize and Benchmark must strictly ascend.");
        }

        /// <summary>
        /// Validates the pre-installed source entries and their content references.
        /// </summary>
        /// <param name="definition">The configuration object.</param>
        /// <param name="jsonPath">The configuration's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        private static void ValidateInitialSource(JsonObject definition, string jsonPath, CatalogValidationContext context)
        {
            if (!context.TryArray(definition, "initialSource", jsonPath, "definition.missing-field", "definition.field-type", out JsonArray entries))
                return;

            for (int index = 0; index < entries.Items.Count; index++)
            {
                string path = jsonPath + ".initialSource[" + index + "]";
                if (entries.Items[index] is not JsonObject entry)
                {
                    context.AddError(path, "definition.not-object", "each initial-source entry must be an object.");
                    continue;
                }

                context.TryInteger(entry, "position", path, "definition.missing-field", "definition.field-type", out _);
                PackageFieldRules.ValidateContentField(context, entry, "content", path, "reference.wrong-kind");
            }
        }

        /// <summary>
        /// Validates the Active Branch constraints when present, returning the declared capacity.
        /// </summary>
        /// <param name="definition">The configuration object.</param>
        /// <param name="jsonPath">The configuration's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        /// <returns>The declared Branch capacity, or zero when no Branch is declared.</returns>
        private static long ValidateActiveBranch(JsonObject definition, string jsonPath, CatalogValidationContext context)
        {
            if (!definition.TryGet("activeBranch", out JsonValue value))
                return 0;

            string path = jsonPath + ".activeBranch";
            if (value is not JsonObject branch)
            {
                context.AddError(path, "definition.field-type", "the Active Branch constraints must be an object.");
                return 0;
            }

            context.TryInteger(branch, "capacity", path, "definition.missing-field", "definition.field-type", out long capacity);
            ValidateContentList(branch, "required", path, context);
            ValidateContentList(branch, "quarantined", path, context);
            return capacity;
        }

        /// <summary>
        /// Validates the Buffer load plan against its policy, returning the policy token.
        /// </summary>
        /// <param name="definition">The configuration object.</param>
        /// <param name="jsonPath">The configuration's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        /// <param name="branchCapacity">The declared Active Branch capacity.</param>
        /// <returns>The policy token, or null when it is absent or unknown.</returns>
        private static string ValidateBufferLoad(
            JsonObject definition,
            string jsonPath,
            CatalogValidationContext context,
            long branchCapacity
        )
        {
            if (!context.TryObject(definition, "bufferLoad", jsonPath, "definition.missing-field", "definition.field-type", out JsonObject load))
                return null;

            string path = jsonPath + ".bufferLoad";
            if (!context.TryString(load, "policy", path, "definition.missing-field", "definition.field-type", out string policy))
                return null;

            if (!_loadPolicies.Contains(policy))
            {
                context.AddError(path + ".policy", "process.unknown-load-policy", "'" + policy + "' is not a Buffer load policy.");
                return null;
            }

            if (policy == "SCRIPTED")
            {
                ValidateContentList(load, "initial", path, context);
                ValidateArrivals(load, path, context);
                return policy;
            }

            context.TryInteger(load, "initialCount", path, "definition.missing-field", "definition.field-type", out long initialCount);
            int arrivalCount = 0;
            if (context.TryArray(load, "arrivalsAfterExecutions", path, "definition.missing-field", "definition.field-type", out JsonArray arrivals))
                arrivalCount = arrivals.Items.Count;

            if (initialCount + arrivalCount > branchCapacity)
                context.AddError(path, "process.drawn-count-exceeds-branch", "a drawn load cannot draw more items than the Active Branch holds.");

            return policy;
        }

        /// <summary>
        /// Validates the scripted arrival moments: each names an execution and carries at least one
        /// item. A moment is not bounded by Buffer capacity, because overflow is legal.
        /// </summary>
        /// <param name="load">The Buffer load object.</param>
        /// <param name="jsonPath">The load's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        private static void ValidateArrivals(JsonObject load, string jsonPath, CatalogValidationContext context)
        {
            if (!context.TryArray(load, "arrivals", jsonPath, "definition.missing-field", "definition.field-type", out JsonArray arrivals))
                return;

            for (int index = 0; index < arrivals.Items.Count; index++)
            {
                string path = jsonPath + ".arrivals[" + index + "]";
                if (arrivals.Items[index] is not JsonObject arrival)
                {
                    context.AddError(path, "definition.not-object", "each arrival moment must be an object.");
                    continue;
                }

                context.TryInteger(arrival, "afterExecution", path, "definition.missing-field", "definition.field-type", out _);
                if (!context.TryArray(arrival, "items", path, "definition.missing-field", "definition.field-type", out JsonArray items))
                    continue;

                if (items.Items.Count == 0)
                    context.AddError(path + ".items", "process.scripted-moment-empty", "a declared arrival moment must carry at least one item.");

                ValidateContentList(arrival, "items", path, context);
            }
        }

        /// <summary>
        /// Validates the exposure plan, which is legal only on a drawn load.
        /// </summary>
        /// <param name="definition">The configuration object.</param>
        /// <param name="jsonPath">The configuration's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        /// <param name="policy">The Buffer load policy token.</param>
        private static void ValidateExposure(
            JsonObject definition,
            string jsonPath,
            CatalogValidationContext context,
            string policy
        )
        {
            if (!definition.TryGet("exposure", out JsonValue value))
                return;

            string path = jsonPath + ".exposure";
            if (policy != "DRAWN")
                context.AddError(path, "process.exposure-without-drawn", "an exposure plan belongs to a drawn Buffer load.");

            if (value is JsonObject exposure)
                ValidateContentList(exposure, "guaranteed", path, context);
            else
                context.AddError(path, "definition.field-type", "the exposure plan must be an object.");
        }

        /// <summary>
        /// Validates a named array of content IDs, registering each as a content reference.
        /// </summary>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The array's key.</param>
        /// <param name="jsonPath">The owner's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        private static void ValidateContentList(
            JsonObject owner,
            string key,
            string jsonPath,
            CatalogValidationContext context
        )
        {
            if (!context.TryArray(owner, key, jsonPath, "definition.missing-field", "definition.field-type", out JsonArray items))
                return;

            for (int index = 0; index < items.Items.Count; index++)
            {
                string path = jsonPath + "." + key + "[" + index + "]";
                if (items.Items[index] is JsonString id)
                    PackageFieldRules.RegisterContentReference(context, path, id.Value, "reference.wrong-kind");
                else
                    context.AddError(path, "definition.field-type", "each content reference must be a string.");
            }
        }
    }
}