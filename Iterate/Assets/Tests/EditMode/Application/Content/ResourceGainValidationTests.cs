using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;
using static Iterate.Application.Content.Tests.CatalogTestFixtures;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the RESOURCE_GAIN operation primitive and the trigger requirement it arrives with. A
    /// BUILD_INTERACTION effect observes an event just as an EXECUTION effect does, so it needs a
    /// trigger for the same reason — an untriggered observer would fire on nothing or on everything.
    /// </summary>
    public sealed class ResourceGainValidationTests
    {
        private const string ManifestFileName = "dependencies.json";

        private const string DependenciesManifest = @"{
            ""revision"": ""0.1.0"",
            ""schemaVersion"": ""1.0.0"",
            ""files"": [ { ""file"": ""dependencies.json"", ""category"": ""DEPENDENCY"" } ]
        }";

        [Test]
        public void AByteGainOnArchive_IsAccepted()
        {
            IReadOnlyList<CatalogError> errors = Validate(DependenciesManifest, (ManifestFileName, Dependency(Gain("BYTES", 1))));

            Assert.AreEqual(0, errors.Count, Describe(errors));
        }

        [Test]
        public void AGainOfTokens_ReportsOperationPayload()
        {
            IReadOnlyList<CatalogError> errors = Validate(DependenciesManifest, (ManifestFileName, Dependency(Gain("TOKENS", 1))));

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.operation-payload"), Describe(errors));
        }

        [Test]
        public void AGainOfZero_ReportsOperationPayload()
        {
            IReadOnlyList<CatalogError> errors = Validate(DependenciesManifest, (ManifestFileName, Dependency(Gain("BYTES", 0))));

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.operation-payload"), Describe(errors));
        }

        [Test]
        public void ABuildInteractionEffectWithoutATrigger_ReportsTriggerRequired()
        {
            string effect = @"{
                ""phaseDomain"": ""BUILD_INTERACTION"",
                ""operation"": { ""kind"": ""RESOURCE_GAIN"", ""resource"": ""BYTES"", ""amount"": 1 },
                ""targeting"": { ""kind"": ""NO_TARGET"", ""argument"": """" },
                ""stacking"": ""INDEPENDENT_RESOLUTION"",
                ""frequency"": { ""allowance"": ""FIRST_QUALIFYING_EVENT"", ""scope"": ""PROCESS"" }
            }";

            IReadOnlyList<CatalogError> errors = Validate(DependenciesManifest, (ManifestFileName, Dependency(effect)));

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.trigger-required"), Describe(errors));
        }

        [Test]
        public void AnExecutionEffectWithoutATrigger_StillReportsTriggerRequired()
        {
            string effect = @"{
                ""phaseDomain"": ""EXECUTION"",
                ""operation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 1 } },
                ""targeting"": { ""kind"": ""NO_TARGET"", ""argument"": """" },
                ""stacking"": ""ADDITIVE_PARAMETER""
            }";

            IReadOnlyList<CatalogError> errors = Validate(DependenciesManifest, (ManifestFileName, Dependency(effect)));

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.trigger-required"), Describe(errors));
        }

        private static string Gain(string resource, int amount)
        {
            return @"{
                ""phaseDomain"": ""BUILD_INTERACTION"",
                ""trigger"": { ""eventFamily"": ""CONTENT_LIFECYCLE"", ""eventSubtype"": ""OBJECT_ARCHIVED"", ""qualifiers"": [] },
                ""operation"": { ""kind"": ""RESOURCE_GAIN"", ""resource"": """ + resource + @""", ""amount"": " + amount + @" },
                ""targeting"": { ""kind"": ""NO_TARGET"", ""argument"": """" },
                ""stacking"": ""INDEPENDENT_RESOLUTION"",
                ""frequency"": { ""allowance"": ""FIRST_QUALIFYING_EVENT"", ""scope"": ""PROCESS"" }
            }";
        }

        private static string Dependency(string effect)
        {
            return @"[
                {
                    ""id"": ""WB-DEP-008"", ""rulesText"": ""x"", ""displayName"": ""GARBAGE COLLECTOR"",
                    ""category"": ""DEPENDENCY"", ""rarity"": ""UNCOMMON"", ""tags"": [""Archive""],
                    ""ram"": 1,
                    ""effects"": [ " + effect + @" ]
                }
            ]";
        }

        private static string Describe(IReadOnlyList<CatalogError> errors)
        {
            System.Text.StringBuilder text = new();
            for (int i = 0; i < errors.Count; i++)
            {
                text.Append(errors[i].RuleName).Append(' ').Append(errors[i].Message).Append('\n');
            }

            return text.ToString();
        }
    }
}
