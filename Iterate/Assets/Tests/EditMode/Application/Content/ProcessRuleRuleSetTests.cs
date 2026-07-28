using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;
using static Iterate.Application.Content.Tests.CatalogTestFixtures;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the Process-rule rule set: the valid THERMAL THROTTLE pair, the manifest category token
    /// the validator dispatches on, and the malformed cases that must reject with a named reason
    /// rather than load silently.
    /// The counter-request operation itself needs no new validation — the shared operation rules
    /// already require the counter, delta, bounds, and bound flags, and the counter vocabulary
    /// already admits HEAT. What is new here is the category: its manifest token, its ID prefix, and
    /// its dispatch to this rule set.
    /// </summary>
    public sealed class ProcessRuleRuleSetTests
    {
        private const string ManifestFileName = "processrules.json";

        private const string ProcessRulesManifest = @"{
            ""revision"": ""0.1.0"",
            ""schemaVersion"": ""1.0.0"",
            ""files"": [ { ""file"": ""processrules.json"", ""category"": ""PROCESS_RULE"" } ]
        }";

        [Test]
        public void Validate_ValidProcessRulePair_ReturnsNoErrors()
        {
            IReadOnlyList<CatalogError> errors = Validate(ProcessRulesManifest, (ManifestFileName, ValidProcessRulesFile));

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void Validate_UnknownCounterName_ReportsOperationPayload()
        {
            string json = ValidProcessRulesFile.Replace(@"""counter"": ""HEAT""", @"""counter"": ""PRESSURE""");

            IReadOnlyList<CatalogError> errors = Validate(ProcessRulesManifest, (ManifestFileName, json));

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.operation-payload"));
        }

        [Test]
        public void Validate_CounterRequestMissingBoundFlag_ReportsOperationPayload()
        {
            string json = ValidProcessRulesFile.Replace(@", ""hasCeiling"": true", string.Empty);

            IReadOnlyList<CatalogError> errors = Validate(ProcessRulesManifest, (ManifestFileName, json));

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.operation-payload"));
        }

        [Test]
        public void Validate_WrongIDPrefix_ReportsIDPattern()
        {
            string json = ValidProcessRulesFile.Replace(@"""WB-PRC-001""", @"""WB-UTL-001""");

            IReadOnlyList<CatalogError> errors = Validate(ProcessRulesManifest, (ManifestFileName, json));

            Assert.Greater(errors.Count, 0);
        }

        [Test]
        public void Validate_WrongCategoryToken_ReportsAnError()
        {
            string json = ValidProcessRulesFile.Replace(@"""category"": ""PROCESS_RULE""", @"""category"": ""UTILITY""");

            IReadOnlyList<CatalogError> errors = Validate(ProcessRulesManifest, (ManifestFileName, json));

            Assert.Greater(errors.Count, 0);
        }

        [Test]
        public void Validate_UnknownTimingBand_ReportsUnknownBand()
        {
            string json = ValidProcessRulesFile.Replace(@"""name"": ""IMMEDIATE_RESULT_REACTION""", @"""name"": ""THERMAL_BAND""");

            IReadOnlyList<CatalogError> errors = Validate(ProcessRulesManifest, (ManifestFileName, json));

            Assert.Greater(errors.Count, 0);
        }

        [Test]
        public void Validate_FileNotArray_ReportsFileNotArray()
        {
            IReadOnlyList<CatalogError> errors = Validate(ProcessRulesManifest, (ManifestFileName, "{}"));

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.file-not-array"));
        }

        [Test]
        public void Validate_UnknownManifestCategory_IsRejected()
        {
            string manifest = ProcessRulesManifest.Replace(@"""PROCESS_RULE""", @"""PROCESS_RULES""");

            IReadOnlyList<CatalogError> errors = Validate(manifest, (ManifestFileName, ValidProcessRulesFile));

            Assert.IsTrue(HasRule(errors, "manifest.unknown-category"));
        }
    }
}
