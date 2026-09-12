using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// Validates the routes file: a route names the Process configuration it runs and the shop that
    /// opens before it, and both references resolve to those kinds.
    /// </summary>
    public sealed class RouteRuleSet : ICategoryRuleSet
    {
        private static readonly ControlledVocabulary _allowedKeys = new(
            "id", "displayName", "process", "shop"
        );

        /// <summary>
        /// The file kind this rule set validates.
        /// </summary>
        public CatalogFileKind Kind => CatalogFileKind.Route;

        /// <summary>
        /// The manifest category token this rule set answers to.
        /// </summary>
        public string CategoryToken => "ROUTE";

        /// <summary>
        /// Validates the routes file's parsed value tree.
        /// </summary>
        /// <param name="fileRoot">The parsed file value tree.</param>
        /// <param name="context">The shared validation context.</param>
        public void ValidateFile(JsonValue fileRoot, CatalogValidationContext context)
        {
            if (fileRoot is not JsonArray array)
            {
                context.AddError("$", "definition.file-not-array", "a routes file must be an array.");
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

                PackageFieldRules.ValidateIdentity(context, definition, path, "ROUTE", _allowedKeys);
                PackageFieldRules.ValidateIDField(context, definition, "process", path, "WB-PROC-", "reference.wrong-kind");
                PackageFieldRules.ValidateIDField(context, definition, "shop", path, "WB-SHOP-", "reference.wrong-kind");
            }
        }
    }
}