using System.Collections.Generic;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Inline JSON building blocks and helpers shared by the catalog-validator tests. Every fixture is
    /// a C# string constant (no <c>.json</c> files); tests perturb exactly one block to isolate a
    /// single rule. Validation always runs end-to-end through <see cref="CatalogValidator"/>.
    /// </summary>
    public static class CatalogTestFixtures
    {
        /// <summary>
        /// The conventional manifest file name the validator stamps on manifest errors.
        /// </summary>
        public const string ManifestFile = "manifest.json";

        /// <summary>
        /// A manifest listing only the Instructions file — the minimal valid catalog's manifest.
        /// </summary>
        public const string InstructionsOnlyManifest = @"{
            ""revision"": ""0.1.0"",
            ""schemaVersion"": ""1.0.0"",
            ""files"": [
                { ""file"": ""instructions.json"", ""category"": ""INSTRUCTION"" }
            ]
        }";

        /// <summary>
        /// Two valid Instruction definitions: a constant operand and a register operand.
        /// </summary>
        public const string ValidInstructionsFile = @"[
            {
                ""id"": ""WB-INS-002"",
                ""rulesText"": ""Value += 2"",
                ""displayName"": ""Value += 2"",
                ""category"": ""INSTRUCTION"",
                ""rarity"": ""COMMON"",
                ""tags"": [""Value"", ""Add"", ""Fixed""],
                ""sourceFootprint"": 1,
                ""primaryOperation"": {
                    ""kind"": ""QUANTITY_CHANGE"",
                    ""register"": ""VALUE"",
                    ""operator"": ""ADD"",
                    ""operand"": { ""source"": ""CONSTANT"", ""constant"": 2 }
                }
            },
            {
                ""id"": ""WB-INS-003"",
                ""rulesText"": ""Value += Signal"",
                ""displayName"": ""Value += Signal"",
                ""category"": ""INSTRUCTION"",
                ""rarity"": ""COMMON"",
                ""tags"": [""Value"", ""Add"", ""Register""],
                ""sourceFootprint"": 1,
                ""primaryOperation"": {
                    ""kind"": ""QUANTITY_CHANGE"",
                    ""register"": ""VALUE"",
                    ""operator"": ""ADD"",
                    ""operand"": { ""source"": ""REGISTER"", ""register"": ""SIGNAL"" }
                }
            }
        ]";

        /// <summary>
        /// A valid Repeat Structure and a valid Condition Structure.
        /// </summary>
        public const string ValidStructuresFile = @"[
            {
                ""id"": ""WB-STR-001"",
                ""rulesText"": ""Repeat 2 times: [1 instruction]"",
                ""displayName"": ""Repeat 2 times: [1 instruction]"",
                ""category"": ""STRUCTURE"",
                ""rarity"": ""COMMON"",
                ""tags"": [""Structure"", ""Repeat""],
                ""sourceFootprint"": 2,
                ""structureKind"": ""REPEAT"",
                ""repeatCount"": 2
            },
            {
                ""id"": ""WB-STR-002"",
                ""rulesText"": ""If Value is even: [1 instruction]"",
                ""displayName"": ""If Value is even: [1 instruction]"",
                ""category"": ""STRUCTURE"",
                ""rarity"": ""COMMON"",
                ""tags"": [""Structure"", ""Condition"", ""Value"", ""Parity""],
                ""sourceFootprint"": 2,
                ""structureKind"": ""CONDITION"",
                ""predicate"": { ""register"": ""VALUE"", ""comparison"": ""IS_EVEN"", ""operand"": 0 }
            }
        ]";

        /// <summary>
        /// A valid Directive definition carrying one EXECUTION effect.
        /// </summary>
        public const string ValidDirectivesFile = @"[
            {
                ""id"": ""WB-DIR-001"",
                ""rulesText"": ""On execution start, Value += 1."",
                ""displayName"": ""SIGNAL ROUTER"",
                ""category"": ""DIRECTIVE"",
                ""rarity"": ""UNCOMMON"",
                ""tags"": [""Value"", ""Add""],
                ""effects"": [
                    {
                        ""phaseDomain"": ""EXECUTION"",
                        ""trigger"": {
                            ""eventFamily"": ""LIFECYCLE"",
                            ""eventSubtype"": ""EXECUTION_STARTED"",
                            ""qualifiers"": [],
                            ""timing"": { ""kind"": ""BAND"", ""name"": ""PRIMARY_OPERATION_RESOLUTION"" }
                        },
                        ""operation"": {
                            ""kind"": ""QUANTITY_CHANGE"",
                            ""register"": ""VALUE"",
                            ""operator"": ""ADD"",
                            ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 }
                        },
                        ""targeting"": { ""kind"": ""NO_TARGET"", ""argument"": """" },
                        ""stacking"": ""INDEPENDENT_RESOLUTION"",
                        ""frequency"": { ""allowance"": ""ONCE"", ""scope"": ""EXECUTION"" }
                    }
                ]
            }
        ]";

        /// <summary>
        /// A valid Dependency definition (STANDARD LIBRARY shape) with one EXECUTION effect.
        /// </summary>
        public const string ValidDependenciesFile = @"[
            {
                ""id"": ""WB-DEP-001"",
                ""rulesText"": ""The first eligible fixed-number addition each execution resolves +1 higher."",
                ""displayName"": ""STANDARD LIBRARY"",
                ""category"": ""DEPENDENCY"",
                ""rarity"": ""STARTER"",
                ""tags"": [""Value"", ""Add"", ""Fixed""],
                ""ram"": 0,
                ""effects"": [
                    {
                        ""phaseDomain"": ""EXECUTION"",
                        ""trigger"": {
                            ""eventFamily"": ""QUANTITY"",
                            ""eventSubtype"": ""PRIMARY_OPERATION_PENDING"",
                            ""qualifiers"": [ { ""kind"": ""OPERATION_CLASS"", ""value"": ""FIXED_ADDITION"" } ],
                            ""timing"": { ""kind"": ""BAND"", ""name"": ""OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION"" }
                        },
                        ""operation"": {
                            ""kind"": ""QUANTITY_CHANGE"",
                            ""register"": ""VALUE"",
                            ""operator"": ""ADD"",
                            ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 }
                        },
                        ""targeting"": { ""kind"": ""SAME_REGISTER_AS_TRIGGER"", ""argument"": """" },
                        ""stacking"": ""INDEPENDENT_RESOLUTION"",
                        ""frequency"": { ""allowance"": ""FIRST_QUALIFYING_EVENT"", ""scope"": ""EXECUTION"" }
                    }
                ]
            }
        ]";

        /// <summary>
        /// A valid Patch definition carrying one EXECUTION effect.
        /// </summary>
        public const string ValidPatchesFile = @"[
            {
                ""id"": ""WB-PAT-001"",
                ""rulesText"": ""The patched Instruction resolves +1 higher."",
                ""displayName"": ""CONSTANT PATCH"",
                ""category"": ""PATCH"",
                ""rarity"": ""COMMON"",
                ""tags"": [""Value"", ""Add""],
                ""hostEligibility"": ""FIXED_NUMBER_ADDITION_HOSTS"",
                ""effects"": [
                    {
                        ""phaseDomain"": ""EXECUTION"",
                        ""trigger"": {
                            ""eventFamily"": ""QUANTITY"",
                            ""eventSubtype"": ""PRIMARY_OPERATION_PENDING"",
                            ""qualifiers"": [],
                            ""timing"": { ""kind"": ""BAND"", ""name"": ""OPERATION_MODIFICATION_REPLACEMENT_OR_PREVENTION"" }
                        },
                        ""operation"": { ""kind"": ""OPERATION_MODIFICATION"", ""operandDelta"": 1 },
                        ""targeting"": { ""kind"": ""OWN_HOST"", ""argument"": """" },
                        ""stacking"": ""ADDITIVE_PARAMETER"",
                        ""frequency"": { ""allowance"": ""ONCE"", ""scope"": ""SOURCE_EXECUTION"" }
                    }
                ]
            }
        ]";

        /// <summary>
        /// A valid Utility definition: a PROCESS_SETUP configuration modification with no trigger.
        /// </summary>
        public const string ValidUtilitiesFile = @"[
            {
                ""id"": ""WB-UTL-001"",
                ""rulesText"": ""Start each Process with +1 Byte."",
                ""displayName"": ""PREFETCH BUFFER"",
                ""category"": ""UTILITY"",
                ""rarity"": ""COMMON"",
                ""tags"": [""Bytes"", ""Setup""],
                ""effects"": [
                    {
                        ""phaseDomain"": ""PROCESS_SETUP"",
                        ""operation"": { ""kind"": ""CONFIGURATION_MODIFICATION"", ""setting"": ""STARTING_BYTES"", ""amount"": 1, ""setsAbsolute"": false },
                        ""stacking"": ""ADDITIVE_PARAMETER""
                    }
                ]
            }
        ]";

        /// <summary>
        /// The 30-row parameter register with every required WB-PAR ID, ratios fractional and the rest
        /// integral.
        /// </summary>
        public const string ValidParametersFile = @"[
            { ""id"": ""WB-PAR-001"", ""name"": ""Systems per Session"", ""value"": 3, ""unit"": ""count"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-002"", ""name"": ""Processes per Session"", ""value"": 12, ""unit"": ""count"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-003"", ""name"": ""Target Session duration minimum"", ""value"": 20, ""unit"": ""minutes"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-004"", ""name"": ""Target Session duration maximum"", ""value"": 30, ""unit"": ""minutes"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-005"", ""name"": ""Starting RAM"", ""value"": 4, ""unit"": ""count"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-006"", ""name"": ""RAM after System 1"", ""value"": 5, ""unit"": ""count"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-007"", ""name"": ""RAM after System 2"", ""value"": 6, ""unit"": ""count"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-008"", ""name"": ""Final RAM"", ""value"": 6, ""unit"": ""count"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-009"", ""name"": ""Standard source capacity"", ""value"": 6, ""unit"": ""count"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-010"", ""name"": ""Standard Buffer capacity"", ""value"": 5, ""unit"": ""count"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-011"", ""name"": ""Standard Process executions"", ""value"": 4, ""unit"": ""count"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-012"", ""name"": ""Standard starting Bytes"", ""value"": 3, ""unit"": ""count"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-013"", ""name"": ""Standard initial Buffer items"", ""value"": 3, ""unit"": ""count"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-014"", ""name"": ""Standard later arrivals"", ""value"": 3, ""unit"": ""count"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-015"", ""name"": ""Standard Active Branch capacity"", ""value"": 9, ""unit"": ""count"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-016"", ""name"": ""Standard natural exposure"", ""value"": 6, ""unit"": ""count"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-017"", ""name"": ""Initial compilation cost"", ""value"": 0, ""unit"": ""bytes"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-018"", ""name"": ""Unchanged compilation cost"", ""value"": 0, ""unit"": ""bytes"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-019"", ""name"": ""First edited compilation cost"", ""value"": 1, ""unit"": ""bytes"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-020"", ""name"": ""Second edited compilation cost"", ""value"": 2, ""unit"": ""bytes"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-021"", ""name"": ""Third edited compilation cost"", ""value"": 3, ""unit"": ""bytes"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-022"", ""name"": ""Pass ratio"", ""value"": 1.0, ""unit"": ""ratio"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-023"", ""name"": ""Optimize ratio"", ""value"": 1.75, ""unit"": ""ratio"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-024"", ""name"": ""Benchmark ratio"", ""value"": 3.0, ""unit"": ""ratio"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-026"", ""name"": ""Standard Benchmark Token bonus"", ""value"": 2, ""unit"": ""tokens"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-028"", ""name"": ""First reroll cost"", ""value"": 1, ""unit"": ""tokens"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-029"", ""name"": ""Second reroll cost"", ""value"": 2, ""unit"": ""tokens"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-030"", ""name"": ""Third reroll cost"", ""value"": 3, ""unit"": ""tokens"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-035"", ""name"": ""Dependency destruction refund ratio"", ""value"": 0.5, ""unit"": ""ratio"", ""scope"": ""GLOBAL"" },
            { ""id"": ""WB-PAR-036"", ""name"": ""Patch sockets per Repository instance"", ""value"": 2, ""unit"": ""count"", ""scope"": ""GLOBAL"" }
        ]";

        /// <summary>
        /// Builds a <see cref="CatalogFileSet"/> by parsing the manifest and each named content file
        /// through the strict reader.
        /// </summary>
        /// <param name="manifestJson">The manifest JSON text.</param>
        /// <param name="files">The named content files as (name, json) pairs.</param>
        /// <returns>The parsed file set ready for validation.</returns>
        public static CatalogFileSet FileSet(string manifestJson, params (string Name, string Json)[] files)
        {
            CatalogJsonReader reader = new();
            JsonValue manifest = reader.Parse(manifestJson);
            Dictionary<string, JsonValue> content = new(System.StringComparer.Ordinal);
            foreach ((string Name, string Json) file in files)
            {
                content[file.Name] = reader.Parse(file.Json);
            }

            return new CatalogFileSet(manifest, content);
        }

        /// <summary>
        /// Validates a manifest and its content files end-to-end.
        /// </summary>
        /// <param name="manifestJson">The manifest JSON text.</param>
        /// <param name="files">The named content files as (name, json) pairs.</param>
        /// <returns>The complete error list.</returns>
        public static IReadOnlyList<CatalogError> Validate(string manifestJson, params (string Name, string Json)[] files)
        {
            return new CatalogValidator().Validate(FileSet(manifestJson, files));
        }

        /// <summary>
        /// Whether the error list contains an error for the given file and rule.
        /// </summary>
        /// <param name="errors">The error list.</param>
        /// <param name="file">The expected file name.</param>
        /// <param name="ruleName">The expected rule name.</param>
        /// <returns>True when a matching error is present.</returns>
        public static bool HasError(IReadOnlyList<CatalogError> errors, string file, string ruleName)
        {
            foreach (CatalogError error in errors)
            {
                if (error.File == file && error.RuleName == ruleName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether the error list contains any error with the given rule name.
        /// </summary>
        /// <param name="errors">The error list.</param>
        /// <param name="ruleName">The expected rule name.</param>
        /// <returns>True when a matching error is present.</returns>
        public static bool HasRule(IReadOnlyList<CatalogError> errors, string ruleName)
        {
            foreach (CatalogError error in errors)
            {
                if (error.RuleName == ruleName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Counts the errors with the given rule name.
        /// </summary>
        /// <param name="errors">The error list.</param>
        /// <param name="ruleName">The rule name to count.</param>
        /// <returns>The number of matching errors.</returns>
        public static int CountRule(IReadOnlyList<CatalogError> errors, string ruleName)
        {
            int count = 0;
            foreach (CatalogError error in errors)
            {
                if (error.RuleName == ruleName)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
