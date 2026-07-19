using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Infrastructure.Content;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// Tests the real <see cref="CatalogDirectorySource"/> over a temporary directory: the manifest and
    /// named files round-trip, and a missing file surfaces an exception naming it.
    /// </summary>
    public sealed class CatalogDirectorySourceTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "IterateCatalog_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void ReadManifestAsync_RoundTripsManifestText()
        {
            File.WriteAllText(Path.Combine(_root, "manifest.json"), "{ \"revision\": \"0.1.0\" }");
            CatalogDirectorySource source = new(_root);

            string text = source.ReadManifestAsync(CancellationToken.None).GetAwaiter().GetResult();

            StringAssert.Contains("0.1.0", text);
        }

        [Test]
        public void ReadFileAsync_RoundTripsNamedFile()
        {
            File.WriteAllText(Path.Combine(_root, "instructions.json"), "[1, 2, 3]");
            CatalogDirectorySource source = new(_root);

            string text = source.ReadFileAsync("instructions.json", CancellationToken.None).GetAwaiter().GetResult();

            StringAssert.Contains("[1, 2, 3]", text);
        }

        [Test]
        public void ReadFileAsync_MissingFile_ThrowsNamingFile()
        {
            CatalogDirectorySource source = new(_root);

            IOException error = Assert.Throws<IOException>(
                () => source.ReadFileAsync("absent.json", CancellationToken.None).GetAwaiter().GetResult()
            );

            StringAssert.Contains("absent.json", error.Message);
        }

        [Test]
        public void ReadManifestAsync_MissingManifest_Throws()
        {
            CatalogDirectorySource source = new(_root);

            Assert.Throws<IOException>(
                () => source.ReadManifestAsync(CancellationToken.None).GetAwaiter().GetResult()
            );
        }
    }
}
