using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;
using static Iterate.Application.Content.Tests.CatalogTestFixtures;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the Dependency rule set and, through it, the shared effect controlled-vocabulary rules:
    /// the RAM field, trigger event-family/subtype/qualifier/timing membership, the timing kind-name
    /// pairing, targeting membership, and the frequency allowance/scope membership plus the canonical
    /// (allowance, scope) pair rule.
    /// </summary>
    public sealed class DependencyRuleSetTests
    {
        private const string ManifestFileName = "dependencies.json";

        private const string DependenciesManifest = @"{
            ""revision"": ""0.1.0"",
            ""schemaVersion"": ""1.0.0"",
            ""files"": [ { ""file"": ""dependencies.json"", ""category"": ""DEPENDENCY"" } ]
        }";

        private static string Dependency(string effectJson)
        {
            return @"[
                {
                    ""id"": ""WB-DEP-001"", ""rulesText"": ""A first fixed addition resolves +1 higher."", ""displayName"": ""STANDARD LIBRARY"",
                    ""category"": ""DEPENDENCY"", ""rarity"": ""STARTER"", ""tags"": [""Value""], ""ram"": 0,
                    ""effects"": [ " + effectJson + @" ]
                }
            ]";
        }

        private static IReadOnlyList<CatalogError> ValidateDependency(string effectJson)
        {
            return Validate(DependenciesManifest, (ManifestFileName, Dependency(effectJson)));
        }

        [Test]
        public void Validate_ValidDependency_ReturnsNoErrors()
        {
            IReadOnlyList<CatalogError> errors = Validate(DependenciesManifest, (ManifestFileName, ValidDependenciesFile));

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void Validate_MissingRAM_ReportsMissingField()
        {
            string json = @"[
                {
                    ""id"": ""WB-DEP-001"", ""rulesText"": ""x"", ""displayName"": ""STANDARD LIBRARY"",
                    ""category"": ""DEPENDENCY"", ""rarity"": ""STARTER"", ""tags"": [""Value""],
                    ""effects"": []
                }
            ]";

            IReadOnlyList<CatalogError> errors = Validate(DependenciesManifest, (ManifestFileName, json));

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.missing-field"));
        }

        [Test]
        public void Validate_UnknownEventFamily_ReportsUnknownEventFamily()
        {
            string effect = @"{
                ""phaseDomain"": ""EXECUTION"",
                ""trigger"": { ""eventFamily"": ""COSMIC"", ""eventSubtype"": ""EXECUTION_STARTED"", ""qualifiers"": [], ""timing"": { ""kind"": ""BAND"", ""name"": ""PRIMARY_OPERATION_RESOLUTION"" } },
                ""operation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } },
                ""targeting"": { ""kind"": ""NO_TARGET"", ""argument"": """" },
                ""stacking"": ""INDEPENDENT_RESOLUTION"",
                ""frequency"": { ""allowance"": ""ONCE"", ""scope"": ""EXECUTION"" }
            }";

            IReadOnlyList<CatalogError> errors = ValidateDependency(effect);

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.unknown-event-family"));
        }

        [Test]
        public void Validate_UnknownEventSubtype_ReportsUnknownSubtype()
        {
            string effect = @"{
                ""phaseDomain"": ""EXECUTION"",
                ""trigger"": { ""eventFamily"": ""LIFECYCLE"", ""eventSubtype"": ""BIG_BANG"", ""qualifiers"": [], ""timing"": { ""kind"": ""BAND"", ""name"": ""PRIMARY_OPERATION_RESOLUTION"" } },
                ""operation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } },
                ""targeting"": { ""kind"": ""NO_TARGET"", ""argument"": """" },
                ""stacking"": ""INDEPENDENT_RESOLUTION"",
                ""frequency"": { ""allowance"": ""ONCE"", ""scope"": ""EXECUTION"" }
            }";

            IReadOnlyList<CatalogError> errors = ValidateDependency(effect);

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.unknown-subtype"));
        }

        [Test]
        public void Validate_TimingKindNameMismatch_ReportsTimingMismatch()
        {
            string effect = @"{
                ""phaseDomain"": ""EXECUTION"",
                ""trigger"": { ""eventFamily"": ""LIFECYCLE"", ""eventSubtype"": ""EXECUTION_STARTED"", ""qualifiers"": [], ""timing"": { ""kind"": ""BAND"", ""name"": ""END_OF_REPEAT_ITERATION"" } },
                ""operation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } },
                ""targeting"": { ""kind"": ""NO_TARGET"", ""argument"": """" },
                ""stacking"": ""INDEPENDENT_RESOLUTION"",
                ""frequency"": { ""allowance"": ""ONCE"", ""scope"": ""EXECUTION"" }
            }";

            IReadOnlyList<CatalogError> errors = ValidateDependency(effect);

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.timing-mismatch"));
        }

        [Test]
        public void Validate_UnknownTargetingKind_ReportsUnknownTargetingKind()
        {
            string effect = @"{
                ""phaseDomain"": ""EXECUTION"",
                ""trigger"": { ""eventFamily"": ""LIFECYCLE"", ""eventSubtype"": ""EXECUTION_STARTED"", ""qualifiers"": [], ""timing"": { ""kind"": ""BAND"", ""name"": ""PRIMARY_OPERATION_RESOLUTION"" } },
                ""operation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } },
                ""targeting"": { ""kind"": ""EVERYONE"", ""argument"": """" },
                ""stacking"": ""INDEPENDENT_RESOLUTION"",
                ""frequency"": { ""allowance"": ""ONCE"", ""scope"": ""EXECUTION"" }
            }";

            IReadOnlyList<CatalogError> errors = ValidateDependency(effect);

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.unknown-targeting-kind"));
        }

        [Test]
        public void Validate_UnknownFrequencyAllowance_ReportsUnknownAllowance()
        {
            string effect = @"{
                ""phaseDomain"": ""EXECUTION"",
                ""trigger"": { ""eventFamily"": ""LIFECYCLE"", ""eventSubtype"": ""EXECUTION_STARTED"", ""qualifiers"": [], ""timing"": { ""kind"": ""BAND"", ""name"": ""PRIMARY_OPERATION_RESOLUTION"" } },
                ""operation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } },
                ""targeting"": { ""kind"": ""NO_TARGET"", ""argument"": """" },
                ""stacking"": ""INDEPENDENT_RESOLUTION"",
                ""frequency"": { ""allowance"": ""SOMETIMES"", ""scope"": ""EXECUTION"" }
            }";

            IReadOnlyList<CatalogError> errors = ValidateDependency(effect);

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.unknown-frequency-allowance"));
        }

        [Test]
        public void Validate_UnknownFrequencyScope_ReportsUnknownScope()
        {
            string effect = @"{
                ""phaseDomain"": ""EXECUTION"",
                ""trigger"": { ""eventFamily"": ""LIFECYCLE"", ""eventSubtype"": ""EXECUTION_STARTED"", ""qualifiers"": [], ""timing"": { ""kind"": ""BAND"", ""name"": ""PRIMARY_OPERATION_RESOLUTION"" } },
                ""operation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } },
                ""targeting"": { ""kind"": ""NO_TARGET"", ""argument"": """" },
                ""stacking"": ""INDEPENDENT_RESOLUTION"",
                ""frequency"": { ""allowance"": ""ONCE"", ""scope"": ""GALAXY"" }
            }";

            IReadOnlyList<CatalogError> errors = ValidateDependency(effect);

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.unknown-frequency-scope"));
        }

        [Test]
        public void Validate_NonCanonicalFrequencyPair_ReportsFrequencyPair()
        {
            string effect = @"{
                ""phaseDomain"": ""EXECUTION"",
                ""trigger"": { ""eventFamily"": ""LIFECYCLE"", ""eventSubtype"": ""EXECUTION_STARTED"", ""qualifiers"": [], ""timing"": { ""kind"": ""BAND"", ""name"": ""PRIMARY_OPERATION_RESOLUTION"" } },
                ""operation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } },
                ""targeting"": { ""kind"": ""NO_TARGET"", ""argument"": """" },
                ""stacking"": ""INDEPENDENT_RESOLUTION"",
                ""frequency"": { ""allowance"": ""EVERY_QUALIFYING_EVENT"", ""scope"": ""EXECUTION"" }
            }";

            IReadOnlyList<CatalogError> errors = ValidateDependency(effect);

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.frequency-pair"));
        }
    }
}
