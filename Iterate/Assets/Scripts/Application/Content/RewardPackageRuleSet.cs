using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// Validates the reward-packages file: each component carries exactly the payload its kind
    /// admits, amount-bearing components award a positive amount, and content-bearing components
    /// reference the kind they award. Document order is resolution order and is preserved, never
    /// summed.
    /// </summary>
    public sealed class RewardPackageRuleSet : ICategoryRuleSet
    {
        private static readonly ControlledVocabulary _allowedKeys = new("id", "displayName", "components");

        private static readonly ControlledVocabulary _allowedComponentKeys = new(
            "tier", "kind", "amount", "reference"
        );

        private static readonly ControlledVocabulary _tiers = new("PASS", "OPTIMIZE", "BENCHMARK");

        private static readonly ControlledVocabulary _componentKinds = new(
            "TOKENS", "POOL_CHOICE", "GUARANTEED_CONTENT", "CACHED_PATCH", "PATCH",
            "ROUTE_TOKEN_BONUS", "RAM_SET"
        );

        private static readonly ControlledVocabulary _amountKinds = new(
            "TOKENS", "ROUTE_TOKEN_BONUS", "RAM_SET"
        );

        /// <summary>
        /// The file kind this rule set validates.
        /// </summary>
        public CatalogFileKind Kind => CatalogFileKind.RewardPackage;

        /// <summary>
        /// The manifest category token this rule set answers to.
        /// </summary>
        public string CategoryToken => "REWARD_PACKAGE";

        /// <summary>
        /// Validates the reward-packages file's parsed value tree.
        /// </summary>
        /// <param name="fileRoot">The parsed file value tree.</param>
        /// <param name="context">The shared validation context.</param>
        public void ValidateFile(JsonValue fileRoot, CatalogValidationContext context)
        {
            if (fileRoot is not JsonArray array)
            {
                context.AddError("$", "definition.file-not-array", "a reward-packages file must be an array.");
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

                PackageFieldRules.ValidateIdentity(context, definition, path, "RWD", _allowedKeys);
                ValidateComponents(definition, path, context);
            }
        }

        /// <summary>
        /// Validates one package's ordered component list.
        /// </summary>
        /// <param name="definition">The package object.</param>
        /// <param name="jsonPath">The package's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        private static void ValidateComponents(JsonObject definition, string jsonPath, CatalogValidationContext context)
        {
            if (!context.TryArray(definition, "components", jsonPath, "definition.missing-field", "definition.field-type", out JsonArray components))
                return;

            for (int index = 0; index < components.Items.Count; index++)
            {
                string path = jsonPath + ".components[" + index + "]";
                if (components.Items[index] is not JsonObject component)
                {
                    context.AddError(path, "definition.not-object", "each reward component must be an object.");
                    continue;
                }

                ValidateComponent(component, path, context);
            }
        }

        /// <summary>
        /// Validates one reward component's tier, kind and payload.
        /// </summary>
        /// <param name="component">The component object.</param>
        /// <param name="jsonPath">The component's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        private static void ValidateComponent(JsonObject component, string jsonPath, CatalogValidationContext context)
        {
            context.RejectUnknownKeys(component, _allowedComponentKeys, jsonPath, "definition.unknown-field");

            if (context.TryString(component, "tier", jsonPath, "definition.missing-field", "definition.field-type", out string tier))
                context.RequireMembership(tier, _tiers, jsonPath + ".tier", "reward.unknown-tier");

            if (!context.TryString(component, "kind", jsonPath, "definition.missing-field", "definition.field-type", out string kind))
                return;

            if (!_componentKinds.Contains(kind))
            {
                context.AddError(jsonPath + ".kind", "reward.unknown-component-kind", "'" + kind + "' is not a reward component kind.");
                return;
            }

            bool hasAmount = component.TryGet("amount", out _);
            bool hasReference = component.TryGet("reference", out _);

            if (_amountKinds.Contains(kind))
            {
                if (hasReference)
                    context.AddError(jsonPath, "reward.component-shape", "an amount-bearing reward component carries no reference.");

                if (!hasAmount)
                {
                    context.AddError(jsonPath, "reward.component-shape", "an amount-bearing reward component requires an amount.");
                    return;
                }

                if (context.TryInteger(component, "amount", jsonPath, "definition.missing-field", "definition.field-type", out long amount) && amount < 1)
                    context.AddError(jsonPath + ".amount", "reward.amount-not-positive", "a reward amount must be positive.");

                return;
            }

            if (hasAmount)
                context.AddError(jsonPath, "reward.component-shape", "a content-bearing reward component carries no amount.");

            if (!hasReference)
            {
                context.AddError(jsonPath, "reward.component-shape", "a content-bearing reward component requires a reference.");
                return;
            }

            switch (kind)
            {
                case "POOL_CHOICE":
                    PackageFieldRules.ValidateIDField(context, component, "reference", jsonPath, "WB-POOL-", "reward.reference-kind-mismatch");
                    return;
                
                case "PATCH" or "CACHED_PATCH":
                    PackageFieldRules.ValidateIDField(context, component, "reference", jsonPath, "WB-PAT-", "reward.reference-kind-mismatch");
                    return;
                
                default:
                    PackageFieldRules.ValidateContentField(context, component, "reference", jsonPath, "reward.reference-kind-mismatch");
                    break;
            }
        }
    }
}