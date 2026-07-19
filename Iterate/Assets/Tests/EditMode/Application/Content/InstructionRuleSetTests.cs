using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;
using static Iterate.Application.Content.Tests.CatalogTestFixtures;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the Instruction rule set and, through it, the shared structural rules every content
    /// category enforces: file shape, required common fields, category match, rarity membership, id
    /// format, and tag validity, plus the Instruction-specific primary-operation shape.
    /// </summary>
    public sealed class InstructionRuleSetTests
    {
        private const string ManifestFileName = "instructions.json";

        private static IReadOnlyList<CatalogError> ValidateInstructions(string instructionsJson)
        {
            return Validate(InstructionsOnlyManifest, (ManifestFileName, instructionsJson));
        }

        [Test]
        public void Validate_FileNotArray_ReportsFileNotArray()
        {
            IReadOnlyList<CatalogError> errors = ValidateInstructions(@"{ ""id"": ""WB-INS-002"" }");

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.file-not-array"));
        }

        [Test]
        public void Validate_DefinitionNotObject_ReportsNotObject()
        {
            IReadOnlyList<CatalogError> errors = ValidateInstructions(@"[ 42 ]");

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.not-object"));
        }

        [Test]
        public void Validate_MissingCommonField_ReportsMissingField()
        {
            string json = @"[
                {
                    ""id"": ""WB-INS-002"", ""displayName"": ""Value += 2"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""COMMON"", ""tags"": [""Value""],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 2 } }
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateInstructions(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.missing-field"));
        }

        [Test]
        public void Validate_WrongFieldType_ReportsFieldType()
        {
            string json = @"[
                {
                    ""id"": ""WB-INS-002"", ""rulesText"": ""Value += 2"", ""displayName"": ""Value += 2"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""COMMON"", ""tags"": [""Value""],
                    ""sourceFootprint"": ""one"",
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 2 } }
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateInstructions(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.field-type"));
        }

        [Test]
        public void Validate_UnknownField_ReportsUnknownField()
        {
            string json = @"[
                {
                    ""id"": ""WB-INS-002"", ""rulesText"": ""Value += 2"", ""displayName"": ""Value += 2"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""COMMON"", ""tags"": [""Value""],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 2 } },
                    ""surpriseField"": true
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateInstructions(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.unknown-field"));
        }

        [Test]
        public void Validate_CategoryMismatch_ReportsCategoryMismatch()
        {
            string json = @"[
                {
                    ""id"": ""WB-INS-002"", ""rulesText"": ""Value += 2"", ""displayName"": ""Value += 2"",
                    ""category"": ""STRUCTURE"", ""rarity"": ""COMMON"", ""tags"": [""Value""],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 2 } }
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateInstructions(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.category-mismatch"));
        }

        [Test]
        public void Validate_UnknownRarity_ReportsUnknownRarity()
        {
            string json = @"[
                {
                    ""id"": ""WB-INS-002"", ""rulesText"": ""Value += 2"", ""displayName"": ""Value += 2"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""MYTHIC"", ""tags"": [""Value""],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 2 } }
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateInstructions(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.unknown-rarity"));
        }

        [Test]
        public void Validate_WrongIDPrefix_ReportsIDFormat()
        {
            string json = @"[
                {
                    ""id"": ""WB-STR-002"", ""rulesText"": ""Value += 2"", ""displayName"": ""Value += 2"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""COMMON"", ""tags"": [""Value""],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 2 } }
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateInstructions(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.id-format"));
        }

        [Test]
        public void Validate_EmptyTag_ReportsEmptyTag()
        {
            string json = @"[
                {
                    ""id"": ""WB-INS-002"", ""rulesText"": ""Value += 2"", ""displayName"": ""Value += 2"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""COMMON"", ""tags"": [""Value"", """"],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 2 } }
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateInstructions(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "definition.empty-tag"));
        }

        [Test]
        public void Validate_PrimaryOperationBadRegister_ReportsOperationPayload()
        {
            string json = @"[
                {
                    ""id"": ""WB-INS-002"", ""rulesText"": ""Value += 2"", ""displayName"": ""Value += 2"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""COMMON"", ""tags"": [""Value""],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""ACCUMULATOR"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 2 } }
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateInstructions(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.operation-payload"));
        }

        [Test]
        public void Validate_PositionQualifierUnknownKind_ReportsUnknownQualifierKind()
        {
            string json = @"[
                {
                    ""id"": ""WB-INS-014"", ""rulesText"": ""Value += 2 on the final line."", ""displayName"": ""Value += 2 (final)"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""RARE"", ""tags"": [""Value""],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 2 } },
                    ""positionQualifier"": { ""kind"": ""SPATIAL"", ""value"": ""FINAL_LINE"" }
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateInstructions(json);

            Assert.IsTrue(HasError(errors, ManifestFileName, "effect.unknown-qualifier-kind"));
        }

        [Test]
        public void Validate_ValidQualifiedInstruction_ReturnsNoErrors()
        {
            string json = @"[
                {
                    ""id"": ""WB-INS-014"", ""rulesText"": ""Value += 2 on the final line."", ""displayName"": ""Value += 2 (final)"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""RARE"", ""tags"": [""Value""],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 2 } },
                    ""positionQualifier"": { ""kind"": ""POSITIONAL"", ""value"": ""FINAL_LINE"" }
                }
            ]";

            IReadOnlyList<CatalogError> errors = ValidateInstructions(json);

            Assert.AreEqual(0, errors.Count);
        }
    }
}
