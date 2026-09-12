using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// Validates the Starter Archetypes file: the starting Repository names resolvable content and
    /// admits duplicates (two instances of one definition are two Repository items), and the starter
    /// Dependency reference resolves to a Dependency. The starter Dependency's zero-RAM invariant is
    /// deliberately not checked here — it needs the Dependency's own row, which is cross-file state
    /// this validator does not carry; the Session factory and the shipped-catalog suite enforce it.
    /// </summary>
    public sealed class StarterArchetypeRuleSet : ICategoryRuleSet
    {
        private static readonly ControlledVocabulary _allowedKeys = new(
            "id", "displayName", "startingRepository", "starterDependency"
        );

        /// <summary>
        /// The file kind this rule set validates.
        /// </summary>
        public CatalogFileKind Kind => CatalogFileKind.StarterArchetype;

        /// <summary>
        /// The manifest category token this rule set answers to.
        /// </summary>
        public string CategoryToken => "STARTER_ARCHETYPE";

        /// <summary>
        /// Validates the Starter Archetypes file's parsed value tree.
        /// </summary>
        /// <param name="fileRoot">The parsed file value tree.</param>
        /// <param name="context">The shared validation context.</param>
        public void ValidateFile(JsonValue fileRoot, CatalogValidationContext context)
        {
            if (fileRoot is not JsonArray array)
            {
                context.AddError("$", "definition.file-not-array", "a Starter Archetypes file must be an array.");
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

                PackageFieldRules.ValidateIdentity(context, definition, path, "ARCH", _allowedKeys);
                PackageFieldRules.ValidateIDField(
                    context,
                    definition,
                    "starterDependency",
                    path,
                    "WB-DEP-",
                    "archetype.starter-dependency-not-dependency"
                );

                ValidateStartingRepository(definition, path, context);
            }
        }

        /// <summary>
        /// Validates the starting Repository list and its content references.
        /// </summary>
        /// <param name="definition">The archetype object.</param>
        /// <param name="jsonPath">The archetype's JSON path.</param>
        /// <param name="context">The shared validation context.</param>
        private static void ValidateStartingRepository(JsonObject definition, string jsonPath, CatalogValidationContext context)
        {
            if (!context.TryArray(definition, "startingRepository", jsonPath, "definition.missing-field", "definition.field-type", out JsonArray items))
                return;

            if (items.Items.Count == 0)
            {
                context.AddError(jsonPath + ".startingRepository", "archetype.starting-repository-empty", "a Starter Archetype must seed at least one Repository item.");
                return;
            }

            for (int index = 0; index < items.Items.Count; index++)
            {
                string path = jsonPath + ".startingRepository[" + index + "]";
                if (items.Items[index] is JsonString id)
                    PackageFieldRules.RegisterContentReference(context, path, id.Value, "reference.wrong-kind");
                else
                    context.AddError(path, "definition.field-type", "each starting-Repository entry must be a string.");
            }
        }
    }
}