using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Application.Logging;
using Iterate.Domain.Content;
using Iterate.Infrastructure.Content;

namespace Iterate.Composition.Root.Tests
{
    /// <summary>
    /// Tests <see cref="CatalogAsyncStartable"/> in isolation (plain construction, no container): a
    /// successful load over the shipped catalog marks the holder Loaded and logs the Info line; a
    /// defective catalog marks the holder Failed and logs an Error carrying the named error list.
    /// </summary>
    public sealed class CatalogAsyncStartableTests
    {
        private static CatalogAsyncStartable Startable(ICatalogFileSource source, CatalogHolder holder, CapturingLogSink sink)
        {
            CatalogLoader loader = new(new CatalogJsonReader(), new CatalogValidator(), new CatalogFreezer(), source);
            GameLoggerFactory loggers = new(sink, new LogFilterConfig(LogLevel.Info));
            return new CatalogAsyncStartable(loader, holder, loggers);
        }

        [Test]
        public void StartAsync_ShippedCatalog_MarksLoadedAndLogsInfo()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Catalog");
            CatalogHolder holder = new();
            CapturingLogSink sink = new();

            Startable(new CatalogDirectorySource(root), holder, sink)
                .StartAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(CatalogState.Loaded, holder.State);
            Assert.IsTrue(holder.TryGetCatalog(out ContentCatalog catalog));
            Assert.AreEqual("0.1.0", catalog.Revision);
            Assert.IsTrue(sink.Has(LogLevel.Info, "Catalog loaded"));
        }

        [Test]
        public void StartAsync_DefectiveCatalog_MarksFailedAndLogsError()
        {
            const string manifest = @"{ ""revision"": ""0.1.0"", ""schemaVersion"": ""1.0.0"", ""files"": [ { ""file"": ""instructions.json"", ""category"": ""INSTRUCTION"" } ] }";
            const string brokenInstructions = @"[
                {
                    ""id"": ""WB-INS-002"", ""rulesText"": ""Value += 2"", ""displayName"": ""Value += 2"",
                    ""category"": ""INSTRUCTION"", ""rarity"": ""MYTHIC"", ""tags"": [""Value""],
                    ""sourceFootprint"": 1,
                    ""primaryOperation"": { ""kind"": ""QUANTITY_CHANGE"", ""register"": ""VALUE"", ""operator"": ""ADD"", ""operand"": { ""source"": ""CONSTANT"", ""constant"": 2 } }
                }
            ]";
            CatalogHolder holder = new();
            CapturingLogSink sink = new();
            Dictionary<string, string> files = new() { ["instructions.json"] = brokenInstructions };

            Startable(new StubFileSource(manifest, files), holder, sink)
                .StartAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(CatalogState.Failed, holder.State);
            bool hasRarityError = false;
            foreach (CatalogError error in holder.Errors)
            {
                if (error.RuleName == "definition.unknown-rarity")
                {
                    hasRarityError = true;
                }
            }

            Assert.IsTrue(hasRarityError, "Expected the holder to carry the validation error.");
            Assert.IsTrue(sink.HasError());
        }

        /// <summary>
        /// A minimal in-memory catalog source for the failure test.
        /// </summary>
        private sealed class StubFileSource : ICatalogFileSource
        {
            private readonly string _manifest;

            private readonly IReadOnlyDictionary<string, string> _files;

            public StubFileSource(string manifest, IReadOnlyDictionary<string, string> files)
            {
                _manifest = manifest;
                _files = files;
            }

            public Task<string> ReadManifestAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(_manifest);
            }

            public Task<string> ReadFileAsync(string fileName, CancellationToken cancellationToken)
            {
                if (_files.TryGetValue(fileName, out string text))
                {
                    return Task.FromResult(text);
                }

                throw new FileNotFoundException("missing: " + fileName);
            }
        }
    }
}
