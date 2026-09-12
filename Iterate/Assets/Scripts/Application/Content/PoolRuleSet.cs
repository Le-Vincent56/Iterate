using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// Validates the acquisition-pools file: the selection method is controlled vocabulary, a
    /// player-choice pool carries a count no larger than its membership, and every member names
    /// resolvable content. Price belongs to the membership, so a member may carry one and a reward
    /// pool's members may not.
    /// </summary>
    public sealed class PoolRuleSet : ICategoryRuleSet
    {
        private static readonly ControlledVocabulary _allowedKeys = new(
            "id", "displayName", "selectionMethod", "selectionCount", "members"
        );

        private static readonly ControlledVocabulary _allowedMemberKeys = new("content", "price");

        /// <summary>
        /// The file kind this rule set validates.
        /// </summary>
        public CatalogFileKind Kind => CatalogFileKind.Pool;

        /// <summary>
        /// The manifest category token this rule set answers to.
        /// </summary>
        public string CategoryToken => "POOL";

        /// <summary>
        /// Validates the acquisition-pools file's parsed value tree.
        /// </summary>
        /// <param name="fileRoot">The parsed file value tree.</param>
        /// <param name="context">The shared validation context.</param>
        public void ValidateFile(JsonValue fileRoot, CatalogValidationContext context)
        {
            if (fileRoot is not JsonArray array)
            {
                context.AddError("$", "definition.file-not-array", "an acquisition-pools file must be an array.");
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

                PackageFieldRules.ValidateIdentity(context, definition, path, "POOL", _allowedKeys);
                ValidatePool(definition, path, context);
            }
        }

        /// <summary>
        /// Validates one pool's selection method, count and membership.
        /// </summary>
        /// <param name="definition">The pool object.</param>
        /// <param name="jsonPath">The pool's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        private static void ValidatePool(JsonObject definition, string jsonPath, CatalogValidationContext context)
        {
            int memberCount = ValidateMembers(definition, jsonPath, context);

            if (!context.TryString(definition, "selectionMethod", jsonPath, "definition.missing-field", "definition.field-type", out string method))
                return;

            if (!CatalogVocabulary.PoolSelectionMethods.Contains(method))
            {
                context.AddError(jsonPath + ".selectionMethod", "pool.unknown-selection-method", "'" + method + "' is not a pool selection method.");
                return;
            }

            bool hasCount = definition.TryGet("selectionCount", out _);
            if (method == "PLAYER_CHOICE" && !hasCount)
            {
                context.AddError(jsonPath, "pool.choice-count-missing", "a player-choice pool must state how many items it offers.");
                return;
            }

            if (!hasCount)
                return;

            if (context.TryInteger(definition, "selectionCount", jsonPath, "definition.missing-field", "definition.field-type", out long count) && count > memberCount)
                context.AddError(jsonPath + ".selectionCount", "pool.count-exceeds-members", "a pool cannot offer more items than it holds.");
        }

        /// <summary>
        /// Validates the pool membership, returning the member count.
        /// </summary>
        /// <param name="definition">The pool object.</param>
        /// <param name="jsonPath">The pool's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        /// <returns>The number of members, or zero when the membership is absent or malformed.</returns>
        private static int ValidateMembers(JsonObject definition, string jsonPath, CatalogValidationContext context)
        {
            if (!context.TryArray(definition, "members", jsonPath, "definition.missing-field", "definition.field-type", out JsonArray members))
                return 0;

            for (int index = 0; index < members.Items.Count; index++)
            {
                string path = jsonPath + ".members[" + index + "]";
                if (members.Items[index] is not JsonObject member)
                {
                    context.AddError(path, "definition.not-object", "each pool member must be an object.");
                    continue;
                }

                context.RejectUnknownKeys(member, _allowedMemberKeys, path, "definition.unknown-field");
                PackageFieldRules.ValidateContentField(context, member, "content", path, "reference.wrong-kind");

                if (member.TryGet("price", out _))
                    context.TryInteger(member, "price", path, "definition.missing-field", "definition.field-type", out _);
            }

            return members.Items.Count;
        }
    }
}