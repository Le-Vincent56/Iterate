using System;
using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// Validates the shops file: a shop with rerolls enabled names the pool a reroll draws from, every
    /// fixed offer names resolvable content, and a shop offering no Dependencies offers none — neither
    /// directly nor through the pool it rerolls from. Reroll costs are parameters, not shop fields, so
    /// nothing here reads a price band.
    public sealed class ShopRuleSet : ICategoryRuleSet
    {
        private static readonly ControlledVocabulary _allowedKeys = new(
            "id", "displayName", "slots", "rerollsEnabled", "pinningEnabled", "dependenciesEnabled",
            "servicesEnabled", "fixedOffers", "rerollPool"
        );

        private static readonly ControlledVocabulary _allowedOfferKeys = new("offerID", "content", "price");

        /// <summary>
        /// The file kind this rule set validates.
        /// </summary>
        public CatalogFileKind Kind => CatalogFileKind.Shop;

        /// <summary>
        /// The manifest category token this rule set answers to.
        /// </summary>
        public string CategoryToken => "SHOP";

        /// <summary>
        /// Validates the shops file's parsed value tree.
        /// </summary>
        /// <param name="fileRoot">The parsed file value tree.</param>
        /// <param name="context">The shared validation context.</param>
        public void ValidateFile(JsonValue fileRoot, CatalogValidationContext context)
        {
            if (fileRoot is not JsonArray array)
            {
                context.AddError("$", "definition.file-not-array", "a shops file must be an array.");
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

                PackageFieldRules.ValidateIdentity(context, definition, path, "SHOP", _allowedKeys);
                ValidateShop(definition, path, context);
            }
        }

        /// <summary>
        /// Validates one shop's switches, reroll pool and fixed offers.
        /// </summary>
        /// <param name="definition">The shop object.</param>
        /// <param name="jsonPath">The shop's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        private static void ValidateShop(JsonObject definition, string jsonPath, CatalogValidationContext context)
        {
            context.TryInteger(definition, "slots", jsonPath, "definition.missing-field", "definition.field-type", out _);
            context.TryBoolean(definition, "pinningEnabled", jsonPath, "definition.missing-field", "definition.field-type", out _);
            context.TryBoolean(definition, "servicesEnabled", jsonPath, "definition.missing-field", "definition.field-type", out _);
            bool offersDependencies = context.TryBoolean(
                definition, 
                "dependenciesEnabled", 
                jsonPath, 
                "definition.missing-field",
                "definition.field-type",
                out bool dependenciesEnabled
            ) && dependenciesEnabled;

            bool hasPool = definition.TryGet("rerollPool", out _);
            if (context.TryBoolean(definition, "rerollsEnabled", jsonPath, "definition.missing-field", "definition.field-type", out bool rerollsEnabled) && rerollsEnabled && !hasPool)
                context.AddError(jsonPath, "shop.reroll-pool-missing", "a shop with rerolls enabled must name a reroll pool.");

            PackageFieldRules.ValidateOptionalIDField(context, definition, "rerollPool", jsonPath, "WB-POOL-", "reference.wrong-kind");

            if (!offersDependencies
                && definition.TryGet("rerollPool", out JsonValue poolValue)
                && poolValue is JsonString pool
                && definition.TryGet("id", out JsonValue idValue)
                && idValue is JsonString shop
            )
            {
                context.RegisterDependencyFreePool(jsonPath + ".rerollPool", pool.Value, shop.Value);
            }
            
            if (!context.TryArray(definition, "fixedOffers", jsonPath, "definition.missing-field", "definition.field-type", out JsonArray offers))
                return;

            for (int index = 0; index < offers.Items.Count; index++)
            {
                string path = jsonPath + ".fixedOffers[" + index + "]";
                if (offers.Items[index] is not JsonObject offer)
                {
                    context.AddError(path, "definition.not-object", "each fixed offer must be an object.");
                    continue;
                }

                context.RejectUnknownKeys(offer, _allowedOfferKeys, path, "definition.unknown-field");
                context.TryString(offer, "offerID", path, "definition.missing-field", "definition.field-type", out _);
                context.TryInteger(offer, "price", path, "definition.missing-field", "definition.field-type", out _);
                PackageFieldRules.ValidateContentField(context, offer, "content", path, "reference.wrong-kind");
                
                if (!offersDependencies
                    && context.TryString(offer, "content", path, "definition.missing-field", "definition.field-type", out string content)
                    && content.StartsWith("WB-DEP-", StringComparison.Ordinal)
                )
                {
                    context.AddError(
                        path + ".content",
                        "shop.dependency-offer-disabled",
                        "'" + content + "' is a Dependency, which this shop does not offer.");
                }
            }
        }
    }
}