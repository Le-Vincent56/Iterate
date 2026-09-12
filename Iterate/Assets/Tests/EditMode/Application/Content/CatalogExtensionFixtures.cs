using System.Collections.Generic;
using Iterate.Application.Content;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Inline JSON building blocks for the eight catalog-extension kinds, on the
    /// <see cref="CatalogTestFixtures"/> idiom: every fixture is a C# string constant, and a test
    /// perturbs exactly one file to isolate a single rule. The valid set cross-references only IDs the
    /// Done item fixtures define, so a happy path resolves every reference and reports nothing.
    /// </summary>
    public static class CatalogExtensionFixtures
    {
        /// <summary>
        /// The Cores file name.
        /// </summary>
        public const string CoresFile = "cores.json";

        /// <summary>
        /// The Process-configurations file name.
        /// </summary>
        public const string ProcessesFile = "processes.json";

        /// <summary>
        /// The shops file name.
        /// </summary>
        public const string ShopsFile = "shops.json";

        /// <summary>
        /// The acquisition-pools file name.
        /// </summary>
        public const string PoolsFile = "pools.json";

        /// <summary>
        /// The reward-packages file name.
        /// </summary>
        public const string RewardsFile = "rewards.json";

        /// <summary>
        /// The routes file name.
        /// </summary>
        public const string RoutesFile = "routes.json";

        /// <summary>
        /// The Starter Archetypes file name.
        /// </summary>
        public const string ArchetypesFile = "archetypes.json";

        /// <summary>
        /// The Systems file name.
        /// </summary>
        public const string SystemsFile = "systems.json";

        /// <summary>
        /// A manifest listing the five Done item files the extension fixtures reference plus all eight
        /// extension files.
        /// </summary>
        public const string ExtensionManifest = @"{
            ""revision"": ""0.2.0"",
            ""schemaVersion"": ""1.0.0"",
            ""files"": [
                { ""file"": ""instructions.json"", ""category"": ""INSTRUCTION"" },
                { ""file"": ""structures.json"", ""category"": ""STRUCTURE"" },
                { ""file"": ""dependencies.json"", ""category"": ""DEPENDENCY"" },
                { ""file"": ""patches.json"", ""category"": ""PATCH"" },
                { ""file"": ""processrules.json"", ""category"": ""PROCESS_RULE"" },
                { ""file"": ""cores.json"", ""category"": ""CORE"" },
                { ""file"": ""processes.json"", ""category"": ""PROCESS_CONFIGURATION"" },
                { ""file"": ""shops.json"", ""category"": ""SHOP"" },
                { ""file"": ""pools.json"", ""category"": ""POOL"" },
                { ""file"": ""rewards.json"", ""category"": ""REWARD_PACKAGE"" },
                { ""file"": ""routes.json"", ""category"": ""ROUTE"" },
                { ""file"": ""archetypes.json"", ""category"": ""STARTER_ARCHETYPE"" },
                { ""file"": ""systems.json"", ""category"": ""SYSTEM"" }
            ]
        }";

        /// <summary>
        /// The extension manifest with the parameter register prepended, for the tests that freeze
        /// rather than only validate: the frozen catalog requires a parameter set.
        /// </summary>
        public const string FreezableManifest = @"{
            ""revision"": ""0.2.0"",
            ""schemaVersion"": ""1.0.0"",
            ""files"": [
                { ""file"": ""parameters.json"", ""category"": ""PARAMETERS"" },
                { ""file"": ""instructions.json"", ""category"": ""INSTRUCTION"" },
                { ""file"": ""structures.json"", ""category"": ""STRUCTURE"" },
                { ""file"": ""dependencies.json"", ""category"": ""DEPENDENCY"" },
                { ""file"": ""patches.json"", ""category"": ""PATCH"" },
                { ""file"": ""processrules.json"", ""category"": ""PROCESS_RULE"" },
                { ""file"": ""cores.json"", ""category"": ""CORE"" },
                { ""file"": ""processes.json"", ""category"": ""PROCESS_CONFIGURATION"" },
                { ""file"": ""shops.json"", ""category"": ""SHOP"" },
                { ""file"": ""pools.json"", ""category"": ""POOL"" },
                { ""file"": ""rewards.json"", ""category"": ""REWARD_PACKAGE"" },
                { ""file"": ""routes.json"", ""category"": ""ROUTE"" },
                { ""file"": ""archetypes.json"", ""category"": ""STARTER_ARCHETYPE"" },
                { ""file"": ""systems.json"", ""category"": ""SYSTEM"" }
            ]
        }";

        /// <summary>
        /// A Cores file whose second Core carries a fixed-Structure line containing a fixed
        /// instruction — the PARITY CHECK shape, which only the freezer tests need.
        /// </summary>
        public const string FixedStructureCoresFile = @"[
            {
                ""id"": ""WB-CORE-001"",
                ""displayName"": ""Tutorial Process 1 Core"",
                ""lines"": [
                    { ""position"": 1, ""kind"": ""FIXED_INSTRUCTION"", ""operation"": { ""operator"": ""ASSIGN"", ""register"": ""VALUE"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } } },
                    { ""position"": 2, ""kind"": ""OPEN"" },
                    { ""position"": 3, ""kind"": ""FIXED_INSTRUCTION"", ""operation"": { ""operator"": ""ADD"", ""register"": ""SCORE"", ""operand"": { ""source"": ""REGISTER"", ""register"": ""VALUE"" } } }
                ],
                ""finalOutputPosition"": 3
            },
            {
                ""id"": ""WB-CORE-003"",
                ""displayName"": ""PARITY CHECK Core"",
                ""lines"": [
                    { ""position"": 1, ""kind"": ""OPEN"" },
                    {
                        ""position"": 2,
                        ""kind"": ""FIXED_STRUCTURE"",
                        ""predicate"": { ""register"": ""VALUE"", ""comparison"": ""IS_EVEN"", ""operand"": 0 },
                        ""contained"": { ""position"": 0, ""kind"": ""FIXED_INSTRUCTION"", ""operation"": { ""operator"": ""ADD"", ""register"": ""SCORE"", ""operand"": { ""source"": ""REGISTER"", ""register"": ""VALUE"" } } }
                    },
                    { ""position"": 3, ""kind"": ""FIXED_INSTRUCTION"", ""operation"": { ""operator"": ""MULTIPLY"", ""register"": ""VALUE"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 2 } } }
                ],
                ""finalOutputPosition"": 3
            }
        ]";

        /// <summary>
        /// One valid Core: a fixed assignment, one open position, and a fixed scoring line designated
        /// as the final output.
        /// </summary>
        public const string ValidCoresFile = @"[
            {
                ""id"": ""WB-CORE-001"",
                ""displayName"": ""Tutorial Process 1 Core"",
                ""lines"": [
                    { ""position"": 1, ""kind"": ""FIXED_INSTRUCTION"", ""operation"": { ""operator"": ""ASSIGN"", ""register"": ""VALUE"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } } },
                    { ""position"": 2, ""kind"": ""OPEN"" },
                    { ""position"": 3, ""kind"": ""FIXED_INSTRUCTION"", ""operation"": { ""operator"": ""ADD"", ""register"": ""SCORE"", ""operand"": { ""source"": ""REGISTER"", ""register"": ""VALUE"" } } }
                ],
                ""finalOutputPosition"": 3
            }
        ]";

        /// <summary>
        /// One valid Process configuration: a scripted tutorial load with one pre-installed source
        /// entry and one arrival moment.
        /// </summary>
        public const string ValidProcessesFile = @"[
            {
                ""id"": ""WB-PROC-001"",
                ""displayName"": ""Tutorial Process 1"",
                ""role"": ""TUTORIAL_1"",
                ""core"": ""WB-CORE-001"",
                ""processRule"": ""WB-PRC-001"",
                ""thresholds"": { ""pass"": 20, ""optimize"": 30, ""benchmark"": 36 },
                ""executions"": 3,
                ""mandatoryExecutions"": false,
                ""startingBytes"": 0,
                ""bufferCapacity"": 0,
                ""sourceCapacity"": 1,
                ""initialSource"": [ { ""position"": 2, ""content"": ""WB-INS-002"" } ],
                ""bufferLoad"": {
                    ""policy"": ""SCRIPTED"",
                    ""initial"": [],
                    ""arrivals"": [ { ""afterExecution"": 1, ""items"": [""WB-INS-003""] } ]
                },
                ""rewardPackage"": ""WB-RWD-001""
            }
        ]";

        /// <summary>
        /// One valid shop: three slots, rerolls disabled, one fixed offer.
        /// </summary>
        public const string ValidShopsFile = @"[
            {
                ""id"": ""WB-SHOP-001"",
                ""displayName"": ""Simplified post-Tutorial-1 shop"",
                ""slots"": 3,
                ""rerollsEnabled"": false,
                ""pinningEnabled"": false,
                ""dependenciesEnabled"": false,
                ""servicesEnabled"": false,
                ""fixedOffers"": [ { ""offerID"": ""OFF-001"", ""content"": ""WB-INS-002"", ""price"": 4 } ]
            }
        ]";

        /// <summary>
        /// One valid player-choice pool of two unpriced members.
        /// </summary>
        public const string ValidPoolsFile = @"[
            {
                ""id"": ""WB-POOL-001"",
                ""displayName"": ""Process 1 fixed reward pool"",
                ""selectionMethod"": ""PLAYER_CHOICE"",
                ""selectionCount"": 1,
                ""members"": [ { ""content"": ""WB-INS-002"" }, { ""content"": ""WB-INS-003"" } ]
            }
        ]";

        /// <summary>
        /// One valid reward package: a Token award, a pool choice and a cached Patch, in resolution
        /// order.
        /// </summary>
        public const string ValidRewardsFile = @"[
            {
                ""id"": ""WB-RWD-001"",
                ""displayName"": ""Tutorial Process 1 rewards"",
                ""components"": [
                    { ""tier"": ""PASS"", ""kind"": ""TOKENS"", ""amount"": 4 },
                    { ""tier"": ""PASS"", ""kind"": ""POOL_CHOICE"", ""reference"": ""WB-POOL-001"" },
                    { ""tier"": ""OPTIMIZE"", ""kind"": ""CACHED_PATCH"", ""reference"": ""WB-PAT-001"" }
                ]
            }
        ]";

        /// <summary>
        /// One valid route pairing a shop with the Process it precedes.
        /// </summary>
        public const string ValidRoutesFile = @"[
            {
                ""id"": ""WB-ROUTE-001"",
                ""displayName"": ""PARITY CHECK"",
                ""process"": ""WB-PROC-001"",
                ""shop"": ""WB-SHOP-001""
            }
        ]";

        /// <summary>
        /// One valid Starter Archetype, listing the same Instruction twice.
        /// </summary>
        public const string ValidArchetypesFile = @"[
            {
                ""id"": ""WB-ARCH-001"",
                ""displayName"": ""Direct Output"",
                ""startingRepository"": [ ""WB-INS-002"", ""WB-INS-002"", ""WB-INS-003"" ],
                ""starterDependency"": ""WB-DEP-001""
            }
        ]";

        /// <summary>
        /// One valid System walking a Process stage, a shop stage and a route-selection stage.
        /// </summary>
        public const string ValidSystemsFile = @"[
            {
                ""id"": ""WB-SYS-001"",
                ""displayName"": ""System 1"",
                ""stages"": [
                    { ""kind"": ""PROCESS"", ""process"": ""WB-PROC-001"" },
                    { ""kind"": ""SHOP"", ""shop"": ""WB-SHOP-001"" },
                    { ""kind"": ""ROUTE_SELECTION"", ""routes"": [ ""WB-ROUTE-001"" ] }
                ]
            }
        ]";

        /// <summary>
        /// Validates the complete valid extension file set with zero or more files replaced.
        /// </summary>
        /// <param name="overrides">The files to replace, as (name, json) pairs.</param>
        /// <returns>The complete error list.</returns>
        public static IReadOnlyList<CatalogError> ValidateWith(params (string Name, string Json)[] overrides)
        {
            Dictionary<string, string> files = new(System.StringComparer.Ordinal)
            {
                { "instructions.json", CatalogTestFixtures.ValidInstructionsFile },
                { "structures.json", CatalogTestFixtures.ValidStructuresFile },
                { "dependencies.json", CatalogTestFixtures.ValidDependenciesFile },
                { "patches.json", CatalogTestFixtures.ValidPatchesFile },
                { "processrules.json", CatalogTestFixtures.ValidProcessRulesFile },
                { CoresFile, ValidCoresFile },
                { ProcessesFile, ValidProcessesFile },
                { ShopsFile, ValidShopsFile },
                { PoolsFile, ValidPoolsFile },
                { RewardsFile, ValidRewardsFile },
                { RoutesFile, ValidRoutesFile },
                { ArchetypesFile, ValidArchetypesFile },
                { SystemsFile, ValidSystemsFile }
            };

            foreach ((string Name, string Json) replacement in overrides)
            {
                files[replacement.Name] = replacement.Json;
            }

            List<(string Name, string Json)> ordered = new(files.Count);
            foreach (KeyValuePair<string, string> file in files)
            {
                ordered.Add((file.Key, file.Value));
            }

            return CatalogTestFixtures.Validate(ExtensionManifest, ordered.ToArray());
        }

        /// <summary>
        /// Builds the freezable file set — the valid extension files plus the parameter register and
        /// the Done item files — with zero or more files replaced, for the tests that load end to end.
        /// </summary>
        /// <param name="overrides">The files to replace, as (name, json) pairs.</param>
        /// <returns>The file texts by name.</returns>
        public static Dictionary<string, string> FreezableFiles(params (string Name, string Json)[] overrides)
        {
            Dictionary<string, string> files = new(System.StringComparer.Ordinal)
            {
                { "parameters.json", CatalogTestFixtures.ValidParametersFile },
                { "instructions.json", CatalogTestFixtures.ValidInstructionsFile },
                { "structures.json", CatalogTestFixtures.ValidStructuresFile },
                { "dependencies.json", CatalogTestFixtures.ValidDependenciesFile },
                { "patches.json", CatalogTestFixtures.ValidPatchesFile },
                { "processrules.json", CatalogTestFixtures.ValidProcessRulesFile },
                { CoresFile, ValidCoresFile },
                { ProcessesFile, ValidProcessesFile },
                { ShopsFile, ValidShopsFile },
                { PoolsFile, ValidPoolsFile },
                { RewardsFile, ValidRewardsFile },
                { RoutesFile, ValidRoutesFile },
                { ArchetypesFile, ValidArchetypesFile },
                { SystemsFile, ValidSystemsFile }
            };

            foreach ((string Name, string Json) replacement in overrides)
            {
                files[replacement.Name] = replacement.Json;
            }

            return files;
        }

        /// <summary>
        /// Counts the errors reported against one file, so a rule set's happy path can assert its own
        /// file is clean rather than only that a named rule is absent.
        /// </summary>
        /// <param name="errors">The error list.</param>
        /// <param name="file">The file name to count errors for.</param>
        /// <returns>The number of errors stamped with that file.</returns>
        public static int CountFile(IReadOnlyList<CatalogError> errors, string file)
        {
            int count = 0;
            foreach (CatalogError error in errors)
            {
                if (error.File == file)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Renders the error list as one line per error, for an assertion failure message that names
        /// what actually went wrong rather than only a count.
        /// </summary>
        /// <param name="errors">The error list.</param>
        /// <returns>The rendered error list.</returns>
        public static string Describe(IReadOnlyList<CatalogError> errors)
        {
            System.Text.StringBuilder builder = new();
            foreach (CatalogError error in errors)
            {
                builder.Append(error.File).Append(' ').Append(error.JsonPath).Append(' ');
                builder.Append(error.RuleName).Append(": ").Append(error.Message).Append('\n');
            }

            return builder.ToString();
        }
    }
}
