using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Logging;
using Iterate.Infrastructure.Telemetry;

namespace Iterate.Infrastructure.Telemetry.Tests
{
    /// <summary>
    /// Tests <see cref="JsonlTelemetryStore"/> in a temporary folder created and deleted by the
    /// suite: the folder is created, a run is readable while open with every line present, listing
    /// strips the extension, a deletion leaves sibling files alone, and a run whose file cannot be
    /// opened gives exactly one Warning and no exception.
    /// </summary>
    public sealed class JsonlTelemetryStoreTests
    {
        private string _parent;
        private string _root;
        private RecordingLogSink _sink;
        private JsonlTelemetryStore _store;

        [SetUp]
        public void SetUp()
        {
            _parent = Path.Combine(Path.GetTempPath(), $"iterate-telemetry-{Guid.NewGuid():N}");
            _root = Path.Combine(_parent, "telemetry");
            _sink = new RecordingLogSink();
            _store = new JsonlTelemetryStore(_root, new GameLoggerFactory(_sink, new LogFilterConfig(LogLevel.Info)));
        }

        [TearDown]
        public void TearDown()
        {
            _store.Close("run-a");
            _store.Close("run-b");
            if (Directory.Exists(_parent))
                Directory.Delete(_parent, true);
        }

        [Test]
        public void Open_CreatesTheFolder()
        {
            _store.Open("run-a");

            Assert.IsTrue(Directory.Exists(_root));
            Assert.IsTrue(File.Exists(Path.Combine(_root, "run-a.jsonl")));
        }

        [Test]
        public void Read_WhileTheRunIsOpen_ReturnsEveryLineSoFar()
        {
            _store.Open("run-a");
            _store.Append("run-a", "{\"seq\":0}\n");
            _store.Append("run-a", "{\"seq\":1}\n");

            string text = _store.ReadAsync("run-a", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual("{\"seq\":0}\n{\"seq\":1}\n", text);
        }

        [Test]
        public void Append_WritesUTF8WithoutAByteOrderMark()
        {
            _store.Open("run-a");
            _store.Append("run-a", "{\"note\":\"é\"}\n");
            _store.Close("run-a");

            byte[] bytes = File.ReadAllBytes(Path.Combine(_root, "run-a.jsonl"));

            Assert.AreEqual((byte)'{', bytes[0]);
            Assert.AreEqual("{\"note\":\"é\"}\n", File.ReadAllText(Path.Combine(_root, "run-a.jsonl")));
        }

        [Test]
        public void List_ReturnsRunIdentitiesWithoutExtensionsAndIgnoresOtherFiles()
        {
            _store.Open("run-b");
            _store.Open("run-a");
            File.WriteAllText(Path.Combine(_root, "meta.json"), "{}");

            IReadOnlyList<string> runs = _store.ListAsync(CancellationToken.None).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "run-a", "run-b" }, runs);
        }

        [Test]
        public void List_WithNoFolder_IsEmpty()
        {
            CollectionAssert.IsEmpty(_store.ListAsync(CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void Delete_RemovesOnlyTheRunsFile()
        {
            _store.Open("run-a");
            _store.Close("run-a");
            File.WriteAllText(Path.Combine(_root, "meta.json"), "{}");
            File.WriteAllText(Path.Combine(_root, "checkpoint.json"), "{}");

            bool deleted = _store.DeleteAsync("run-a", CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(deleted);
            Assert.IsFalse(File.Exists(Path.Combine(_root, "run-a.jsonl")));
            Assert.IsTrue(File.Exists(Path.Combine(_root, "meta.json")));
            Assert.IsTrue(File.Exists(Path.Combine(_root, "checkpoint.json")));
            Assert.IsFalse(_store.DeleteAsync("run-a", CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void UnopenableRun_GivesOneWarningAndThrowsNothing()
        {
            // A read-only folder cannot be made reliably on Windows, so the run's own file is held
            // open exclusively instead: opening it for append then fails with a sharing violation.
            Directory.CreateDirectory(_root);
            using (new FileStream(Path.Combine(_root, "run-a.jsonl"), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Assert.DoesNotThrow(() => _store.Open("run-a"));
                Assert.DoesNotThrow(() => _store.Append("run-a", "{}\n"));
                Assert.DoesNotThrow(() => _store.Append("run-a", "{}\n"));
                Assert.DoesNotThrow(() => _store.Close("run-a"));
                Assert.DoesNotThrow(() => _store.Open("run-a"));
            }

            Assert.AreEqual(1, _sink.CountAt(LogLevel.Warning));
            Assert.AreEqual(LogCategories.Telemetry, _sink.Entries[0].Category);
            Assert.AreEqual(0, new FileInfo(Path.Combine(_root, "run-a.jsonl")).Length, "The dropped lines were never written.");
        }

        [Test]
        public void WriteExport_CreatesItsFolderAndWritesTheText()
        {
            string destination = Path.Combine(_parent, "exports", "run-a.jsonl");

            _store.WriteExportAsync(destination, "{\"type\":\"export\"}\n", CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual("{\"type\":\"export\"}\n", File.ReadAllText(destination));
        }
    }
}
