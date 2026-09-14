using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;
using static Iterate.Application.Content.Tests.CatalogTestFixtures;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the Patch rule set: the required host-eligibility field, a valid Patch, and an
    /// operation-specific payload defect surfaced through a Patch effect.
    /// </summary>
    public sealed class PatchRuleSetTests
    {
        private const string ManifestFileName = "patches.json";

        private const string PatchesManifest = @"{
            ""revision"": ""0.1.0"",
            ""schemaVersion"": ""1.0.0"",
            ""files"": [ { ""file"": ""patches.json"", ""category"": ""PATCH"" } ]
        }";

        [Test]
        public void Validate_ValidPatch_ReturnsNoErrors()
        {
            IReadOnlyList<CatalogError> errors = Validate(PatchesManifest, (ManifestFileName, ValidPatchesFile));

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void Validate_MissingHostEligibility_ReportsMissingField()
        {
            string json = @"[
                {
                    ""id"": ""WB-PAT-001"", ""rulesText"": ""x"", ""displayName"": ""CONSTANT PATCH"",
                    ""category"": ""PATCH"", ""rarity"": ""COMMON"", ""tags"": [""Value""],
                    ""effects"": [
                        { ""phaseDomain"": ""EXECUTION"", ""operation"": { ""kind"": ""OPERATION_MODIFICATION"", ""operandDelta"": 1 }, ""targeting"": { ""kind"": ""OWN_HOST"", ""argument"": """" }, ""stacking"": ""ADDITIVE_PARAMETER"" }
                    ]
                }
            ]";

            IReadOnlyList<CatalogError> errors = Validate(PatchesManifest, (ManifestFileName, json));

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.missing-field"));
        }

        [Test]
        public void Validate_UnknownHostEligibility_ReportsUnknownHostEligibility()
        {
            string json = @"[
                {
                    ""id"": ""WB-PAT-001"", ""rulesText"": ""x"", ""displayName"": ""CONSTANT PATCH"",
                    ""category"": ""PATCH"", ""rarity"": ""COMMON"", ""tags"": [""Value""],
                    ""hostEligibility"": ""NO_SUCH_RULE"",
                    ""effects"": [
                        { ""phaseDomain"": ""EXECUTION"", ""operation"": { ""kind"": ""OPERATION_MODIFICATION"", ""operandDelta"": 1 }, ""targeting"": { ""kind"": ""OWN_HOST"", ""argument"": """" }, ""stacking"": ""ADDITIVE_PARAMETER"" }
                    ]
                }
            ]";

            IReadOnlyList<CatalogError> errors = Validate(PatchesManifest, (ManifestFileName, json));

            Assert.IsTrue(HasError(errors, ManifestFileName, "patch.unknown-host-eligibility"));
        }

        [Test]
        public void Validate_EveryVocabularyRule_IsAccepted()
        {
            foreach (string rule in Iterate.Domain.Content.CatalogVocabulary.PatchHostEligibilityRules)
            {
                string json = @"[
                    {
                        ""id"": ""WB-PAT-001"", ""rulesText"": ""x"", ""displayName"": ""CONSTANT PATCH"",
                        ""category"": ""PATCH"", ""rarity"": ""COMMON"", ""tags"": [""Value""],
                        ""hostEligibility"": """ + rule + @""",
                        ""effects"": [
                            { ""phaseDomain"": ""EXECUTION"", ""operation"": { ""kind"": ""OPERATION_MODIFICATION"", ""operandDelta"": 1 }, ""targeting"": { ""kind"": ""OWN_HOST"", ""argument"": """" }, ""stacking"": ""ADDITIVE_PARAMETER"" }
                        ]
                    }
                ]";

                IReadOnlyList<CatalogError> errors = Validate(PatchesManifest, (ManifestFileName, json));

                Assert.IsFalse(
                    HasError(errors, ManifestFileName, "patch.unknown-host-eligibility"),
                    rule + " is in the vocabulary but rejected by the rule set");
            }
        }

        [Test]
        public void Validate_OperationModificationMissingDelta_ReportsOperationPayload()
        {
            string json = @"[
                {
                    ""id"": ""WB-PAT-001"", ""rulesText"": ""x"", ""displayName"": ""CONSTANT PATCH"",
                    ""category"": ""PATCH"", ""rarity"": ""COMMON"", ""tags"": [""Value""],
                    ""hostEligibility"": ""FIXED_NUMBER_ADDITION_HOSTS"",
                    ""effects"": [
                        { ""phaseDomain"": ""EXECUTION"", ""operation"": { ""kind"": ""OPERATION_MODIFICATION"" }, ""targeting"": { ""kind"": ""OWN_HOST"", ""argument"": """" }, ""stacking"": ""ADDITIVE_PARAMETER"" }
                    ]
                }
            ]";

            IReadOnlyList<CatalogError> errors = Validate(PatchesManifest, (ManifestFileName, json));

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.operation-payload"));
        }
    }
}
