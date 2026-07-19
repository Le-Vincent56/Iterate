using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;
using static Iterate.Application.Content.Tests.CatalogTestFixtures;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the Directive rule set and, through it, the shared effect structural rules: the effects
    /// array, per-effect required fields, phase-domain membership, unknown effect fields, the
    /// EXECUTION trigger requirement, and operation-kind membership.
    /// </summary>
    public sealed class DirectiveRuleSetTests
    {
        private const string ManifestFileName = "directives.json";

        private const string DirectivesManifest = @"{
            ""revision"": ""0.1.0"",
            ""schemaVersion"": ""1.0.0"",
            ""files"": [ { ""file"": ""directives.json"", ""category"": ""DIRECTIVE"" } ]
        }";

        private static string Directive(string effectsJson)
        {
            return @"[
                {
                    ""id"": ""WB-DIR-001"", ""rulesText"": ""On execution start, Value += 1."", ""displayName"": ""SIGNAL ROUTER"",
                    ""category"": ""DIRECTIVE"", ""rarity"": ""UNCOMMON"", ""tags"": [""Value""],
                    ""effects"": " + effectsJson + @"
                }
            ]";
        }

        private static IReadOnlyList<CatalogError> ValidateDirective(string effectsJson)
        {
            return Validate(DirectivesManifest, (ManifestFileName, Directive(effectsJson)));
        }

        [Test]
        public void Validate_ValidDirective_ReturnsNoErrors()
        {
            IReadOnlyList<CatalogError> errors = Validate(DirectivesManifest, (ManifestFileName, ValidDirectivesFile));

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void Validate_MissingEffects_ReportsMissingField()
        {
            string json = @"[
                {
                    ""id"": ""WB-DIR-001"", ""rulesText"": ""Does nothing."", ""displayName"": ""NO OP"",
                    ""category"": ""DIRECTIVE"", ""rarity"": ""UNCOMMON"", ""tags"": [""Value""]
                }
            ]";

            IReadOnlyList<CatalogError> errors = Validate(DirectivesManifest, (ManifestFileName, json));

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.missing-field"));
        }

        [Test]
        public void Validate_EffectNotObject_ReportsEffectNotObject()
        {
            IReadOnlyList<CatalogError> errors = ValidateDirective(@"[ 7 ]");

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.not-object"));
        }

        [Test]
        public void Validate_EffectMissingOperation_ReportsEffectMissingField()
        {
            IReadOnlyList<CatalogError> errors = ValidateDirective(@"[ { ""phaseDomain"": ""PROCESS_SETUP"", ""stacking"": ""ADDITIVE_PARAMETER"" } ]");

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.missing-field"));
        }

        [Test]
        public void Validate_EffectUnknownPhaseDomain_ReportsUnknownPhaseDomain()
        {
            string effect = @"[
                {
                    ""phaseDomain"": ""TELEPORTATION"",
                    ""operation"": { ""kind"": ""OPERATION_MODIFICATION"", ""operandDelta"": 1 },
                    ""stacking"": ""ADDITIVE_PARAMETER""
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateDirective(effect);

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.unknown-phase-domain"));
        }

        [Test]
        public void Validate_EffectUnknownField_ReportsUnknownField()
        {
            string effect = @"[
                {
                    ""phaseDomain"": ""PROCESS_SETUP"",
                    ""operation"": { ""kind"": ""OPERATION_MODIFICATION"", ""operandDelta"": 1 },
                    ""stacking"": ""ADDITIVE_PARAMETER"",
                    ""mystery"": 1
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateDirective(effect);

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.unknown-field"));
        }

        [Test]
        public void Validate_ExecutionEffectWithoutTrigger_ReportsTriggerRequired()
        {
            string effect = @"[
                {
                    ""phaseDomain"": ""EXECUTION"",
                    ""operation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } },
                    ""targeting"": { ""kind"": ""NO_TARGET"", ""argument"": """" },
                    ""stacking"": ""INDEPENDENT_RESOLUTION"",
                    ""frequency"": { ""allowance"": ""ONCE"", ""scope"": ""EXECUTION"" }
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateDirective(effect);

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.trigger-required"));
        }

        [Test]
        public void Validate_EffectUnknownOperationKind_ReportsUnknownOperationKind()
        {
            string effect = @"[
                {
                    ""phaseDomain"": ""PROCESS_SETUP"",
                    ""operation"": { ""kind"": ""TIME_TRAVEL"" },
                    ""stacking"": ""ADDITIVE_PARAMETER""
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateDirective(effect);

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.unknown-operation-kind"));
        }

        [Test]
        public void Validate_EffectUnknownStacking_ReportsUnknownStacking()
        {
            string effect = @"[
                {
                    ""phaseDomain"": ""PROCESS_SETUP"",
                    ""operation"": { ""kind"": ""OPERATION_MODIFICATION"", ""operandDelta"": 1 },
                    ""stacking"": ""MERGE_ALL""
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateDirective(effect);

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.unknown-stacking"));
        }
    }
}
