using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// A per-file-kind validation strategy. Each rule set validates one catalog file end-to-end,
    /// reporting failures into the shared context and registering its definitions for the cross-cutting
    /// rules. Adding a content category is one rule-set class plus one registry entry; the shared rules
    /// never reopen.
    /// </summary>
    public interface ICategoryRuleSet
    {
        /// <summary>
        /// The file kind this rule set validates.
        /// </summary>
        CatalogFileKind Kind { get; }

        /// <summary>
        /// The manifest category token this rule set answers to.
        /// </summary>
        string CategoryToken { get; }

        /// <summary>
        /// Validates one catalog file's parsed value tree, reporting failures into the context.
        /// </summary>
        /// <param name="fileRoot">The parsed file value tree.</param>
        /// <param name="context">The shared validation context.</param>
        void ValidateFile(JsonValue fileRoot, CatalogValidationContext context);
    }
}