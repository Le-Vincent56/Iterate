using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;
using static Iterate.Application.Content.Tests.CatalogTestFixtures;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the Utility rule set: a valid PROCESS_SETUP configuration modification (trigger legitimately
    /// omitted) and an unknown configuration setting surfaced as an operation-payload defect.
    /// </summary>
    public sealed class UtilityRuleSetTests
    {
        private const string ManifestFileName = "utilities.json";

        private const string UtilitiesManifest = @"{
            ""revision"": ""0.1.0"",
            ""schemaVersion"": ""1.0.0"",
            ""files"": [ { ""file"": ""utilities.json"", ""category"": ""UTILITY"" } ]
        }";

        [Test]
        public void Validate_ValidProcessSetupUtility_ReturnsNoErrors()
        {
            IReadOnlyList<CatalogError> errors = Validate(UtilitiesManifest, (ManifestFileName, ValidUtilitiesFile));

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void Validate_UnknownConfigurationSetting_ReportsOperationPayload()
        {
            string json = @"[
                {
                    ""id"": ""WB-UTL-001"", ""rulesText"": ""x"", ""displayName"": ""PREFETCH BUFFER"",
                    ""category"": ""UTILITY"", ""rarity"": ""COMMON"", ""tags"": [""Bytes""],
                    ""effects"": [
                        { ""phaseDomain"": ""PROCESS_SETUP"", ""operation"": { ""kind"": ""CONFIGURATION_MODIFICATION"", ""setting"": ""OVERCLOCK"", ""amount"": 1, ""setsAbsolute"": false }, ""stacking"": ""ADDITIVE_PARAMETER"" }
                    ]
                }
            ]";

            IReadOnlyList<CatalogError> errors = Validate(UtilitiesManifest, (ManifestFileName, json));

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.operation-payload"));
        }
    }
}
