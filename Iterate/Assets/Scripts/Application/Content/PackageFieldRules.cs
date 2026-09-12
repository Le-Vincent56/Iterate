using System;
using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// The identity and reference rules the eight package and configuration kinds share. Packages are
    /// not items: they carry an id and a display name and none of the item fields, so the item-shaped
    /// common-field pass does not apply to them. One home for the shared pass, so eight rule sets do
    /// not each restate it.
    /// </summary>
    public static class PackageFieldRules
    {
        /// <summary>
        /// The WB namespaces a content reference may name: the three source-object kinds plus the
        /// three installable kinds a shop, pool, reward or archetype may hand the player.
        /// </summary>
        public static readonly ControlledVocabulary ContentNamespaces = new(
            "WB-INS-", "WB-STR-", "WB-DIR-", "WB-PAT-", "WB-UTL-", "WB-DEP-"
        );

        /// <summary>
        /// The prefix an ID reference passes when any canon ID is acceptable and only existence is
        /// being checked.
        /// </summary>
        public const string AnyCanonID = "WB-";

        /// <summary>
        /// Validates a package record's key set and identity and registers it for the catalog-wide
        /// rules.
        /// </summary>
        /// <param name="context">The shared validation context.</param>
        /// <param name="definition">The record object.</param>
        /// <param name="jsonPath">The record's JSON path.</param>
        /// <param name="idPrefix">The expected WB ID category prefix, such as "CORE".</param>
        /// <param name="allowedKeys">The record's allowed key set.</param>
        public static void ValidateIdentity(
            CatalogValidationContext context,
            JsonObject definition,
            string jsonPath,
            string idPrefix,
            ControlledVocabulary allowedKeys
        )
        {
            context.RejectUnknownKeys(definition, allowedKeys, jsonPath, "definition.unknown-field");

            string id = null;
            if (context.TryString(definition, "id", jsonPath, "definition.missing-field", "definition.field-type", out string idValue))
            {
                id = idValue;
                if (!HasCategoryPrefix(idValue, idPrefix))
                    context.AddError(jsonPath, "definition.id-format", "the id '" + idValue + "' must match WB-" + idPrefix + "-### for its category.");
            }

            string displayName = null;
            if (context.TryString(definition, "displayName", jsonPath, "definition.missing-field", "definition.field-type", out string displayValue))
                displayName = displayValue;

            if (id != null)
                context.RegisterDefinition(id, displayName ?? id, jsonPath);
        }

        /// <summary>
        /// Registers a reference to player-facing content, whose kind is not fixed by the referencing
        /// site: the namespace is checked against the content namespaces here, and existence is
        /// resolved after every file is read.
        /// </summary>
        /// <param name="context">The shared validation context.</param>
        /// <param name="jsonPath">The reference's JSON path.</param>
        /// <param name="referencedID">The referenced content ID.</param>
        /// <param name="ruleName">The rule to report when the ID is not in a content namespace.</param>
        public static void RegisterContentReference(
            CatalogValidationContext context,
            string jsonPath,
            string referencedID,
            string ruleName
        )
        {
            if (!IsContentNamespace(referencedID))
            {
                context.AddError(jsonPath, ruleName, "'" + referencedID + "' is not a content id.");
                return;
            }

            context.RegisterIDReference(jsonPath, referencedID, AnyCanonID, ruleName);
        }

        /// <summary>
        /// Reads a required string field and registers it as a content reference when present.
        /// </summary>
        /// <param name="context">The shared validation context.</param>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The field key.</param>
        /// <param name="jsonPath">The owner's JSON path.</param>
        /// <param name="ruleName">The rule to report when the ID is not in a content namespace.</param>
        public static void ValidateContentField(
            CatalogValidationContext context,
            JsonObject owner,
            string key,
            string jsonPath,
            string ruleName
        )
        {
            if (context.TryString(owner, key, jsonPath, "definition.missing-field", "definition.field-type", out string value))
                RegisterContentReference(context, jsonPath + "." + key, value, ruleName);
        }

        /// <summary>
        /// Reads a required ID field and registers it as a reference expecting one namespace.
        /// </summary>
        /// <param name="context">The shared validation context.</param>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The field key.</param>
        /// <param name="jsonPath">The owner's JSON path.</param>
        /// <param name="expectedPrefix">The expected WB namespace prefix.</param>
        /// <param name="ruleName">The rule to report when the namespace does not match.</param>
        public static void ValidateIDField(
            CatalogValidationContext context,
            JsonObject owner,
            string key,
            string jsonPath,
            string expectedPrefix,
            string ruleName
        )
        {
            if (context.TryString(owner, key, jsonPath, "definition.missing-field", "definition.field-type", out string value))
                context.RegisterIDReference(jsonPath + "." + key, value, expectedPrefix, ruleName);
        }

        /// <summary>
        /// Reads an optional ID field and registers it as a reference when it is present.
        /// </summary>
        /// <param name="context">The shared validation context.</param>
        /// <param name="owner">The owning object.</param>
        /// <param name="key">The field key.</param>
        /// <param name="jsonPath">The owner's JSON path.</param>
        /// <param name="expectedPrefix">The expected WB namespace prefix.</param>
        /// <param name="ruleName">The rule to report when the namespace does not match.</param>
        public static void ValidateOptionalIDField(
            CatalogValidationContext context,
            JsonObject owner,
            string key,
            string jsonPath,
            string expectedPrefix,
            string ruleName
        )
        {
            if (!owner.TryGet(key, out _))
                return;

            ValidateIDField(context, owner, key, jsonPath, expectedPrefix, ruleName);
        }

        /// <summary>
        /// Whether an ID begins with one of the content namespaces.
        /// </summary>
        /// <param name="id">The candidate ID.</param>
        /// <returns>True when the ID names player-facing content.</returns>
        private static bool IsContentNamespace(string id)
        {
            foreach (string prefix in ContentNamespaces)
            {
                if (id.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Whether an ID matches the WB-&lt;prefix&gt;-### shape for its category. Mirrors the
        /// item-side check, which is private to the validation context.
        /// </summary>
        /// <param name="id">The candidate ID.</param>
        /// <param name="prefix">The category prefix, such as "CORE".</param>
        /// <returns>True when the ID matches the category's shape.</returns>
        private static bool HasCategoryPrefix(string id, string prefix)
        {
            string expected = "WB-" + prefix + "-";
            if (!id.StartsWith(expected, StringComparison.Ordinal))
                return false;

            string tail = id.Substring(expected.Length);
            if (tail.Length != 3)
                return false;

            for (int index = 0; index < tail.Length; index++)
            {
                char digit = tail[index];
                if (digit is < '0' or > '9')
                    return false;
            }

            return true;
        }
    }
}