using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Domain.Values;
using static Iterate.Application.Content.Tests.CatalogTestFixtures;

namespace Iterate.Application.Content.Tests
{
    /// <summary>
    /// Tests the <see cref="CatalogLoader"/> read → parse → validate → freeze pipeline: a valid catalog
    /// freezes to a <see cref="Iterate.Domain.Content.ContentCatalog"/>; validation defects, unreadable
    /// and unparseable files, and a freezer escape all surface as a <see cref="CatalogLoadException"/>
    /// carrying the complete, file-named error list.
    /// </summary>
    public sealed class CatalogLoaderTests
    {
        private const string FullManifest = @"{
            ""revision"": ""0.1.0"",
            ""schemaVersion"": ""1.0.0"",
            ""files"": [
                { ""file"": ""parameters.json"", ""category"": ""PARAMETERS"" },
                { ""file"": ""instructions.json"", ""category"": ""INSTRUCTION"" },
                { ""file"": ""directives.json"", ""category"": ""DIRECTIVE"" }
            ]
        }";

        private static Iterate.Domain.Content.ContentCatalog Load(
            ICatalogValidator validator,
            string manifest,
            IReadOnlyDictionary<string, string> files
        )
        {
            FakeCatalogFileSource source = new(manifest, files);
            CatalogLoader loader = new(new CatalogJsonReader(), validator, new CatalogFreezer(), source);
            return loader.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        [Test]
        public void LoadAsync_ValidCatalog_FreezesContentCatalog()
        {
            Dictionary<string, string> files = new()
            {
                ["parameters.json"] = ValidParametersFile,
                ["instructions.json"] = ValidInstructionsFile,
                ["directives.json"] = ValidDirectivesFile
            };

            Iterate.Domain.Content.ContentCatalog catalog = Load(new CatalogValidator(), FullManifest, files);

            Assert.AreEqual("0.1.0", catalog.Revision);
            Assert.AreEqual(4, catalog.Parameters.StartingRAM);
            Assert.AreEqual(2, catalog.Instructions.Count);
            Assert.AreEqual(1, catalog.Directives.Count);
            Assert.IsTrue(catalog.TryGetInstruction(new InstructionID("WB-INS-002"), out _));
        }

        [Test]
        public void LoadAsync_ValidationDefect_ThrowsWithNamedError()
        {
            string brokenInstructions = @"[
                {
                    ""id"": ""WB-INS-002"", ""rulesText"": ""Value += 2"", ""displayName"": ""Value += 2"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""MYTHIC"", ""tags"": [""Value""],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 2 } }
                }
            ]";
            Dictionary<string, string> files = new()
            {
                ["instructions.json"] = brokenInstructions
            };

            CatalogLoadException error = Assert.Throws<CatalogLoadException>(
                () => Load(new CatalogValidator(), InstructionsOnlyManifest, files)
            );

            Assert.IsTrue(HasError(error.Errors, "instructions.json", "definition.unknown-rarity"));
            StringAssert.Contains("instructions.json", error.Message);
        }

        [Test]
        public void LoadAsync_UnreadableFile_ReportsFileUnreadable()
        {
            Dictionary<string, string> files = new();
            FakeCatalogFileSource source = new(
                InstructionsOnlyManifest,
                files,
                false,
                new[] { "instructions.json" }
            );
            CatalogLoader loader = new(new CatalogJsonReader(), new CatalogValidator(), new CatalogFreezer(), source);

            CatalogLoadException error = Assert.Throws<CatalogLoadException>(
                () => loader.LoadAsync(CancellationToken.None).GetAwaiter().GetResult()
            );

            Assert.IsTrue(HasError(error.Errors, "instructions.json", "file.unreadable"));
        }

        [Test]
        public void LoadAsync_UnparseableFile_ReportsFileUnparseable()
        {
            Dictionary<string, string> files = new()
            {
                ["instructions.json"] = "{ not valid json"
            };

            CatalogLoadException error = Assert.Throws<CatalogLoadException>(
                () => Load(new CatalogValidator(), InstructionsOnlyManifest, files)
            );

            Assert.IsTrue(HasError(error.Errors, "instructions.json", "file.unparseable"));
        }

        [Test]
        public void LoadAsync_UnreadableManifest_ReportsFileUnreadable()
        {
            Dictionary<string, string> files = new();
            FakeCatalogFileSource source = new(string.Empty, files, true, null);
            CatalogLoader loader = new(new CatalogJsonReader(), new CatalogValidator(), new CatalogFreezer(), source);

            CatalogLoadException error = Assert.Throws<CatalogLoadException>(
                () => loader.LoadAsync(CancellationToken.None).GetAwaiter().GetResult()
            );

            Assert.IsTrue(HasError(error.Errors, "manifest.json", "file.unreadable"));
        }

        [Test]
        public void LoadAsync_FreezerEscape_ReportsFreezeUnexpectedNamingFile()
        {
            // The instruction parses but omits its operand; a stub validator lets it reach the freezer,
            // which chokes and self-wraps into freeze.unexpected naming the offending file.
            string manifest = @"{
                ""revision"": ""0.1.0"",
                ""schemaVersion"": ""1.0.0"",
                ""files"": [
                    { ""file"": ""parameters.json"", ""category"": ""PARAMETERS"" },
                    { ""file"": ""instructions.json"", ""category"": ""INSTRUCTION"" }
                ]
            }";
            string brokenInstruction = @"[
                {
                    ""id"": ""WB-INS-002"", ""rulesText"": ""Value += 2"", ""displayName"": ""Value += 2"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""COMMON"", ""tags"": [""Value""],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"" }
                }
            ]";
            Dictionary<string, string> files = new()
            {
                ["parameters.json"] = ValidParametersFile,
                ["instructions.json"] = brokenInstruction
            };

            CatalogLoadException error = Assert.Throws<CatalogLoadException>(
                () => Load(new PassThroughValidator(), manifest, files)
            );

            Assert.IsTrue(HasError(error.Errors, "instructions.json", "freeze.unexpected"));
        }

        /// <summary>
        /// A validator stub that reports no errors, letting malformed-but-parseable input reach the
        /// freezer so the loader's freezer-escape wrapping can be exercised.
        /// </summary>
        private sealed class PassThroughValidator : ICatalogValidator
        {
            public IReadOnlyList<CatalogError> Validate(CatalogFileSet files)
            {
                return System.Array.Empty<CatalogError>();
            }
        }
    }
}
