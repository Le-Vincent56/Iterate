using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.Content;
using static Iterate.Application.Content.Tests.CatalogTestFixtures;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the cross-cutting <see cref="CatalogValidator"/> rules: the minimal valid catalog, the
    /// manifest rule group, catalog-wide duplicate IDs, cross-references, and the complete-error-list
    /// contract (two independent defects both surface).
    /// </summary>
    public sealed class CatalogValidatorTests
    {
        [Test]
        public void Validate_MinimalValidCatalog_ReturnsNoErrors()
        {
            IReadOnlyList<CatalogError> errors = Validate(
                InstructionsOnlyManifest,
                ("instructions.json", ValidInstructionsFile)
            );

            Assert.AreEqual(0, errors.Count);
        }

        [Test]
        public void Validate_FullValidCatalog_ReturnsNoErrors()
        {
            string manifest = @"{
                ""revision"": ""0.1.0"",
                ""schemaVersion"": ""1.0.0"",
                ""files"": [
                    { ""file"": ""parameters.json"", ""category"": ""PARAMETERS"" },
                    { ""file"": ""instructions.json"", ""category"": ""INSTRUCTION"" },
                    { ""file"": ""structures.json"", ""category"": ""STRUCTURE"" },
                    { ""file"": ""directives.json"", ""category"": ""DIRECTIVE"" },
                    { ""file"": ""dependencies.json"", ""category"": ""DEPENDENCY"" },
                    { ""file"": ""patches.json"", ""category"": ""PATCH"" },
                    { ""file"": ""utilities.json"", ""category"": ""UTILITY"" }
                ]
            }";

            IReadOnlyList<CatalogError> errors = Validate(
                manifest,
                ("parameters.json", ValidParametersFile),
                ("instructions.json", ValidInstructionsFile),
                ("structures.json", ValidStructuresFile),
                ("directives.json", ValidDirectivesFile),
                ("dependencies.json", ValidDependenciesFile),
                ("patches.json", ValidPatchesFile),
                ("utilities.json", ValidUtilitiesFile)
            );

            Assert.AreEqual(0, errors.Count, "Expected a clean full catalog to validate with no errors.");
        }

        [Test]
        public void Validate_ManifestNotObject_ReportsNotObject()
        {
            IReadOnlyList<CatalogError> errors = Validate("[]");

            Assert.IsTrue(HasError(errors, ManifestFile, "manifest.not-object"));
        }

        [Test]
        public void Validate_ManifestMissingRevision_ReportsMissingField()
        {
            string manifest = @"{ ""schemaVersion"": ""1.0.0"", ""files"": [] }";

            IReadOnlyList<CatalogError> errors = Validate(manifest);

            Assert.IsTrue(HasError(errors, ManifestFile, "manifest.missing-field"));
        }

        [Test]
        public void Validate_ManifestEmptyRevision_ReportsEmptyRevision()
        {
            string manifest = @"{ ""revision"": """", ""schemaVersion"": ""1.0.0"", ""files"": [] }";

            IReadOnlyList<CatalogError> errors = Validate(manifest);

            Assert.IsTrue(HasError(errors, ManifestFile, "manifest.revision-empty"));
        }

        [Test]
        public void Validate_ManifestEntryNotObject_ReportsEntryInvalid()
        {
            string manifest = @"{ ""revision"": ""0.1.0"", ""schemaVersion"": ""1.0.0"", ""files"": [ ""instructions.json"" ] }";

            IReadOnlyList<CatalogError> errors = Validate(manifest);

            Assert.IsTrue(HasError(errors, ManifestFile, "manifest.entry-invalid"));
        }

        [Test]
        public void Validate_ManifestUnknownCategory_ReportsUnknownCategory()
        {
            string manifest = @"{
                ""revision"": ""0.1.0"",
                ""schemaVersion"": ""1.0.0"",
                ""files"": [ { ""file"": ""mystery.json"", ""category"": ""MYSTERY"" } ]
            }";

            IReadOnlyList<CatalogError> errors = Validate(manifest, ("mystery.json", "[]"));

            Assert.IsTrue(HasError(errors, ManifestFile, "manifest.unknown-category"));
        }

        [Test]
        public void Validate_ManifestDuplicateFile_ReportsDuplicateFile()
        {
            string manifest = @"{
                ""revision"": ""0.1.0"",
                ""schemaVersion"": ""1.0.0"",
                ""files"": [
                    { ""file"": ""instructions.json"", ""category"": ""INSTRUCTION"" },
                    { ""file"": ""instructions.json"", ""category"": ""INSTRUCTION"" }
                ]
            }";

            IReadOnlyList<CatalogError> errors = Validate(manifest, ("instructions.json", ValidInstructionsFile));

            Assert.IsTrue(HasError(errors, ManifestFile, "manifest.duplicate-file"));
        }

        [Test]
        public void Validate_ManifestListsAbsentFile_ReportsFileMissing()
        {
            IReadOnlyList<CatalogError> errors = Validate(InstructionsOnlyManifest);

            Assert.IsTrue(HasError(errors, ManifestFile, "manifest.file-missing"));
        }

        [Test]
        public void Validate_UnlistedContentFile_ReportsUnlistedFile()
        {
            IReadOnlyList<CatalogError> errors = Validate(
                InstructionsOnlyManifest,
                ("instructions.json", ValidInstructionsFile),
                ("structures.json", ValidStructuresFile)
            );

            Assert.IsTrue(HasError(errors, ManifestFile, "manifest.unlisted-file"));
        }

        [Test]
        public void Validate_DuplicateIDAcrossCatalog_ReportsDuplicateID()
        {
            string duplicated = @"[
                {
                    ""id"": ""WB-INS-002"", ""rulesText"": ""Value += 2"", ""displayName"": ""Value += 2"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""COMMON"", ""tags"": [""Value""],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 2 } }
                },
                {
                    ""id"": ""WB-INS-002"", ""rulesText"": ""Value += 2"", ""displayName"": ""Value += 2 (dup)"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""COMMON"", ""tags"": [""Value""],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 2 } }
                }
            ]";

            IReadOnlyList<CatalogError> errors = Validate(InstructionsOnlyManifest, ("instructions.json", duplicated));

            Assert.IsTrue(HasError(errors, "instructions.json", "definition.duplicate-id"));
        }

        [Test]
        public void Validate_UnresolvedIneligibilityReference_ReportsCrossRef()
        {
            string instruction = @"[
                {
                    ""id"": ""WB-INS-015"", ""rulesText"": ""Value += the current line number."", ""displayName"": ""Value += line"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""RARE"", ""tags"": [""Value"", ""Add"", ""LineNumber""],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""LINE_NUMBER"" } },
                    ""ineligibilityTags"": [""STANDARD LIBRARY""]
                }
            ]";

            IReadOnlyList<CatalogError> errors = Validate(InstructionsOnlyManifest, ("instructions.json", instruction));

            Assert.IsTrue(HasError(errors, "instructions.json", "crossref.unresolved"));
        }

        [Test]
        public void Validate_ResolvedIneligibilityReference_ReturnsNoCrossRef()
        {
            string manifest = @"{
                ""revision"": ""0.1.0"",
                ""schemaVersion"": ""1.0.0"",
                ""files"": [
                    { ""file"": ""instructions.json"", ""category"": ""INSTRUCTION"" },
                    { ""file"": ""dependencies.json"", ""category"": ""DEPENDENCY"" }
                ]
            }";
            string instruction = @"[
                {
                    ""id"": ""WB-INS-015"", ""rulesText"": ""Value += the current line number."", ""displayName"": ""Value += line"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""RARE"", ""tags"": [""Value"", ""Add"", ""LineNumber""],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""LINE_NUMBER"" } },
                    ""ineligibilityTags"": [""STANDARD LIBRARY""]
                }
            ]";

            IReadOnlyList<CatalogError> errors = Validate(
                manifest,
                ("instructions.json", instruction),
                ("dependencies.json", ValidDependenciesFile)
            );

            Assert.IsFalse(HasRule(errors, "crossref.unresolved"));
        }

        [Test]
        public void Validate_TwoIndependentDefects_ReturnsBoth()
        {
            string broken = @"[
                {
                    ""id"": ""WB-INS-002"", ""rulesText"": ""Value += 2"", ""displayName"": ""Value += 2"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""LEGENDARY"", ""tags"": [""Value""],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 2 } }
                },
                {
                    ""id"": ""INS-003"", ""rulesText"": ""Value += 3"", ""displayName"": ""Value += 3"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""COMMON"", ""tags"": [""Value""],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 3 } }
                }
            ]";

            IReadOnlyList<CatalogError> errors = Validate(InstructionsOnlyManifest, ("instructions.json", broken));

            Assert.IsTrue(HasRule(errors, "definition.unknown-rarity"), "Expected the bad rarity to be reported.");
            Assert.IsTrue(HasRule(errors, "definition.id-format"), "Expected the malformed id to be reported.");
            Assert.GreaterOrEqual(errors.Count, 2, "Validation must not stop at the first failure.");
        }
    }
}
