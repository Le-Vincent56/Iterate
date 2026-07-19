using System;
using System.Collections.Generic;
using Iterate.Application.Content.Json;
using Iterate.Domain.Content;

namespace Iterate.Application.Content
{
    /// <summary>
    /// Validates the parameters file: exactly the 30 WB-PAR register IDs (no missing, unknown, or
    /// duplicate), the integer discipline for non-ratio parameters, and each row's shape. The 30
    /// required IDs and the four ratio IDs mirror <c>ParameterSet</c> (open finding 4).
    /// </summary>
    public sealed class ParameterRuleSet : ICategoryRuleSet
    {
        private static readonly ControlledVocabulary _allowedKeys = new(
            "id", "name", "value", "unit", "scope"
        );

        private static readonly HashSet<string> _requiredIDs = new(StringComparer.Ordinal)
        {
            "WB-PAR-001", "WB-PAR-002", "WB-PAR-003", "WB-PAR-004", "WB-PAR-005",
            "WB-PAR-006", "WB-PAR-007", "WB-PAR-008", "WB-PAR-009", "WB-PAR-010",
            "WB-PAR-011", "WB-PAR-012", "WB-PAR-013", "WB-PAR-014", "WB-PAR-015",
            "WB-PAR-016", "WB-PAR-017", "WB-PAR-018", "WB-PAR-019", "WB-PAR-020",
            "WB-PAR-021", "WB-PAR-022", "WB-PAR-023", "WB-PAR-024", "WB-PAR-026",
            "WB-PAR-028", "WB-PAR-029", "WB-PAR-030", "WB-PAR-035", "WB-PAR-036"
        };

        private static readonly HashSet<string> _ratioIDs = new(StringComparer.Ordinal)
        {
            "WB-PAR-022", "WB-PAR-023", "WB-PAR-024", "WB-PAR-035"
        };

        /// <summary>
        /// The file kind this rule set validates.
        /// </summary>
        public CatalogFileKind Kind => CatalogFileKind.Parameters;

        /// <summary>
        /// The manifest category token this rule set answers to.
        /// </summary>
        public string CategoryToken => "PARAMETERS";

        /// <summary>
        /// Validates the parameters file's parsed value tree.
        /// </summary>
        /// <param name="fileRoot">The parsed file value tree.</param>
        /// <param name="context">The shared validation context.</param>
        public void ValidateFile(JsonValue fileRoot, CatalogValidationContext context)
        {
            if (fileRoot is not JsonArray array)
            {
                context.AddError("$", "parameters.file-not-array", "a parameters file must be an array.");
                return;
            }

            HashSet<string> seen = new(StringComparer.Ordinal);
            for (int index = 0; index < array.Items.Count; index++)
            {
                string path = "$[" + index + "]";
                if (array.Items[index] is not JsonObject row)
                {
                    context.AddError(path, "parameters.row-invalid", "each parameter row must be an object.");
                    continue;
                }

                context.RejectUnknownKeys(row, _allowedKeys, path, "parameters.row-invalid");
                bool hasID = context.TryString(row, "id", path, "parameters.row-invalid", "parameters.row-invalid", out string id);
                context.TryString(row, "name", path, "parameters.row-invalid", "parameters.row-invalid", out _);
                context.TryString(row, "unit", path, "parameters.row-invalid", "parameters.row-invalid", out _);
                context.TryString(row, "scope", path, "parameters.row-invalid", "parameters.row-invalid", out _);
                bool hasValue = context.TryNumber(row, "value", path, "parameters.row-invalid", "parameters.row-invalid", out JsonNumber value);
                if (!hasID) continue;

                if (!_requiredIDs.Contains(id))
                {
                    context.AddError(path, "parameters.unknown-id", "'" + id + "' is not a WB-PAR register ID.");
                    continue;
                }

                if (!seen.Add(id))
                    context.AddError(path, "parameters.duplicate-id", "the register ID '" + id + "' appears more than once.");

                if (hasValue && !_ratioIDs.Contains(id) && !value.IsInteger)
                    context.AddError(path + ".value", "parameters.non-integer", "the integer parameter '" + id + "' carries a fractional value.");
            }

            foreach (string requiredID in _requiredIDs)
            {
                if (!seen.Contains(requiredID))
                    context.AddError("$", "parameters.missing-id", "the register is missing required ID " + requiredID + ".");
            }
        }
    }
}