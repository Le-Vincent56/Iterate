using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;
using static Iterate.Application.Content.Tests.CatalogTestFixtures;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the Structure rule set: the Repeat/Condition field-pairing shape and the valid shapes of
    /// both kinds.
    /// </summary>
    public sealed class StructureRuleSetTests
    {
        private const string ManifestFileName = "structures.json";

        private const string StructuresManifest = @"{
            ""revision"": ""0.1.0"",
            ""schemaVersion"": ""1.0.0"",
            ""files"": [ { ""file"": ""structures.json"", ""category"": ""STRUCTURE"" } ]
        }";

        private static IReadOnlyList<CatalogError> ValidateStructures(string structuresJson)
        {
            return Validate(StructuresManifest, (ManifestFileName, structuresJson));
        }

        [Test]
        public void Validate_ValidRepeatAndCondition_ReturnsNoErrors()
        {
            IReadOnlyList<CatalogError> errors = ValidateStructures(ValidStructuresFile);

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void Validate_RepeatWithPredicate_ReportsStructurePairing()
        {
            string json = @"[
                {
                    ""id"": ""WB-STR-001"", ""rulesText"": ""Repeat 2 times: [1 instruction]"", ""displayName"": ""Repeat 2 times"",
                    ""category"": ""STRUCTURE"", ""rarity"": ""COMMON"", ""tags"": [""Structure"", ""Repeat""],
                    ""sourceFootprint"": 2, ""structureKind"": ""REPEAT"", ""repeatCount"": 2,
                    ""predicate"": { ""register"": ""VALUE"", ""comparison"": ""IS_EVEN"", ""operand"": 0 }
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateStructures(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.structure-pairing"));
        }

        [Test]
        public void Validate_ConditionWithoutPredicate_ReportsStructurePairing()
        {
            string json = @"[
                {
                    ""id"": ""WB-STR-002"", ""rulesText"": ""If Value is even: [1 instruction]"", ""displayName"": ""If even"",
                    ""category"": ""STRUCTURE"", ""rarity"": ""COMMON"", ""tags"": [""Structure"", ""Condition""],
                    ""sourceFootprint"": 2, ""structureKind"": ""CONDITION""
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateStructures(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.structure-pairing"));
        }

        [Test]
        public void Validate_RepeatWithZeroCount_ReportsStructurePairing()
        {
            string json = @"[
                {
                    ""id"": ""WB-STR-001"", ""rulesText"": ""Repeat 0 times: [1 instruction]"", ""displayName"": ""Repeat 0"",
                    ""category"": ""STRUCTURE"", ""rarity"": ""COMMON"", ""tags"": [""Structure"", ""Repeat""],
                    ""sourceFootprint"": 2, ""structureKind"": ""REPEAT"", ""repeatCount"": 0
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateStructures(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.structure-pairing"));
        }

        [Test]
        public void Validate_UnknownStructureKind_ReportsFieldType()
        {
            string json = @"[
                {
                    ""id"": ""WB-STR-001"", ""rulesText"": ""Loop once"", ""displayName"": ""Loop"",
                    ""category"": ""STRUCTURE"", ""rarity"": ""COMMON"", ""tags"": [""Structure""],
                    ""sourceFootprint"": 2, ""structureKind"": ""WHILE"", ""repeatCount"": 2
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateStructures(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.field-type"));
        }

        [Test]
        public void Validate_ConditionUnknownComparison_ReportsFieldType()
        {
            string json = @"[
                {
                    ""id"": ""WB-STR-002"", ""rulesText"": ""If Value is prime"", ""displayName"": ""If prime"",
                    ""category"": ""STRUCTURE"", ""rarity"": ""COMMON"", ""tags"": [""Structure"", ""Condition""],
                    ""sourceFootprint"": 2, ""structureKind"": ""CONDITION"",
                    ""predicate"": { ""register"": ""VALUE"", ""comparison"": ""IS_PRIME"", ""operand"": 0 }
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateStructures(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.field-type"));
        }
    }
}
