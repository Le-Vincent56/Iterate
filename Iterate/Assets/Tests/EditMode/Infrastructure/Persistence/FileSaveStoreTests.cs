using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Persistence;
using Iterate.Infrastructure.Persistence;

namespace Iterate.Infrastructure.Persistence.Tests
{
    /// <summary>
    /// Tests the real <see cref="FileSaveStore"/> over a temporary directory: text round-trips, a missing
    /// file is an ordinary absence rather than a failure, and an unusable path becomes an outcome instead
    /// of an exception. Only cancellation throws.
    ///
    /// The write path is temp-and-rename, so the two things worth proving are that a completed write
    /// leaves no stray temporary file behind and that a failed one does not either — a save that half
    /// happened would be worse than one that did not happen at all.
    /// </summary>
    public sealed class FileSaveStoreTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "IterateSaves_" + Guid.NewGuid().ToString("N"));
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
        public void Read_OfAnAbsentFile_IsAbsent()
        {
            SaveFileReadResult result = Read(new FileSaveStore(_root), SaveFiles.Settings);

            Assert.AreEqual(SaveFileReadOutcome.Absent, result.Outcome);
            Assert.IsNull(result.Text);
            Assert.IsNull(result.Defect);
        }

        [Test]
        public void WriteThenRead_RoundTripsTheText()
        {
            FileSaveStore store = new(_root);

            SaveFileWriteResult written = Write(store, SaveFiles.Meta, "{\n  \"formatVersion\": 1\n}\n");
            SaveFileReadResult read = Read(store, SaveFiles.Meta);

            Assert.IsTrue(written.Succeeded);
            Assert.IsNull(written.Defect);
            Assert.AreEqual(SaveFileReadOutcome.Found, read.Outcome);
            Assert.AreEqual("{\n  \"formatVersion\": 1\n}\n", read.Text);
        }

        [Test]
        public void Write_CreatesAMissingRootDirectory()
        {
            string missing = Path.Combine(_root, "nested", "saves");
            FileSaveStore store = new(missing);

            SaveFileWriteResult result = Write(store, SaveFiles.Meta, "text");

            Assert.IsTrue(result.Succeeded);
            Assert.IsTrue(File.Exists(Path.Combine(missing, SaveFiles.Meta)));
        }

        [Test]
        public void Write_LeavesNoTemporaryFileBehind()
        {
            FileSaveStore store = new(_root);

            Write(store, SaveFiles.Meta, "text");

            AssertNoTemporaryFiles();
        }

        [Test]
        public void Write_ASecondTime_ReplacesTheContent()
        {
            FileSaveStore store = new(_root);
            Write(store, SaveFiles.Meta, "first");

            SaveFileWriteResult second = Write(store, SaveFiles.Meta, "second");

            Assert.IsTrue(second.Succeeded);
            Assert.AreEqual("second", Read(store, SaveFiles.Meta).Text);
            AssertNoTemporaryFiles();
        }

        [Test]
        public void Write_IsUtf8WithoutAByteOrderMark()
        {
            // A byte-order mark would be read back as an unescaped character before the opening brace,
            // and the reader skips exactly one — so this is pinned rather than left to the default.
            FileSaveStore store = new(_root);

            Write(store, SaveFiles.Meta, "{}");

            byte[] bytes = File.ReadAllBytes(Path.Combine(_root, SaveFiles.Meta));
            Assert.AreEqual((byte)'{', bytes[0]);
            Assert.AreEqual(2, bytes.Length);
        }

        [Test]
        public void Read_WhenADirectorySitsWhereTheFileShouldBe_IsUnreadable()
        {
            Directory.CreateDirectory(Path.Combine(_root, SaveFiles.Settings));
            FileSaveStore store = new(_root);

            SaveFileReadResult result = Read(store, SaveFiles.Settings);

            Assert.AreEqual(SaveFileReadOutcome.Unreadable, result.Outcome);
            Assert.IsNotNull(result.Defect);
            Assert.IsNotEmpty(result.Defect);
        }

        [Test]
        public void Write_WhenADirectorySitsWhereTheFileShouldBe_FailsAndLeavesNoTemporaryFile()
        {
            Directory.CreateDirectory(Path.Combine(_root, SaveFiles.Settings));
            FileSaveStore store = new(_root);

            SaveFileWriteResult result = Write(store, SaveFiles.Settings, "text");

            Assert.IsFalse(result.Succeeded);
            Assert.IsNotNull(result.Defect);
            Assert.IsNotEmpty(result.Defect);
            AssertNoTemporaryFiles();
        }

        [Test]
        public void Delete_RemovesTheFileThenReportsItAbsent()
        {
            FileSaveStore store = new(_root);
            Write(store, SaveFiles.Checkpoint, "text");

            SaveFileDeleteResult first = Delete(store, SaveFiles.Checkpoint);
            SaveFileDeleteResult second = Delete(store, SaveFiles.Checkpoint);

            Assert.AreEqual(SaveFileDeleteOutcome.Deleted, first.Outcome);
            Assert.AreEqual(SaveFileDeleteOutcome.Absent, second.Outcome);
            Assert.IsFalse(File.Exists(Path.Combine(_root, SaveFiles.Checkpoint)));
        }

        [Test]
        public void Delete_WhenADirectorySitsWhereTheFileShouldBe_Fails()
        {
            Directory.CreateDirectory(Path.Combine(_root, SaveFiles.Checkpoint));
            FileSaveStore store = new(_root);

            SaveFileDeleteResult result = Delete(store, SaveFiles.Checkpoint);

            Assert.AreEqual(SaveFileDeleteOutcome.Failed, result.Outcome);
            Assert.IsNotEmpty(result.Defect);
        }

        [Test]
        public void Write_WithAPreCancelledToken_ThrowsAndLeavesNoFile()
        {
            FileSaveStore store = new(_root);
            CancellationTokenSource source = new();
            source.Cancel();

            Assert.CatchAsync<OperationCanceledException>(
                async () => await store.WriteAsync(SaveFiles.Meta, "text", source.Token)
            );
            Assert.IsFalse(File.Exists(Path.Combine(_root, SaveFiles.Meta)));
            AssertNoTemporaryFiles();
        }

        [Test]
        public void Read_WithAPreCancelledToken_Throws()
        {
            FileSaveStore store = new(_root);
            Write(store, SaveFiles.Meta, "text");
            CancellationTokenSource source = new();
            source.Cancel();

            Assert.CatchAsync<OperationCanceledException>(
                async () => await store.ReadAsync(SaveFiles.Meta, source.Token)
            );
        }

        [Test]
        public void Delete_WithAPreCancelledToken_Throws()
        {
            FileSaveStore store = new(_root);
            Write(store, SaveFiles.Checkpoint, "text");
            CancellationTokenSource source = new();
            source.Cancel();

            Assert.CatchAsync<OperationCanceledException>(
                async () => await store.DeleteAsync(SaveFiles.Checkpoint, source.Token)
            );
            Assert.IsTrue(File.Exists(Path.Combine(_root, SaveFiles.Checkpoint)), "a cancelled delete removes nothing.");
        }

        [Test]
        public void ThreeFiles_AreIndependent()
        {
            FileSaveStore store = new(_root);

            Write(store, SaveFiles.Settings, "s");
            Write(store, SaveFiles.Meta, "m");
            Write(store, SaveFiles.Checkpoint, "c");
            Delete(store, SaveFiles.Checkpoint);

            Assert.AreEqual("s", Read(store, SaveFiles.Settings).Text);
            Assert.AreEqual("m", Read(store, SaveFiles.Meta).Text);
            Assert.AreEqual(SaveFileReadOutcome.Absent, Read(store, SaveFiles.Checkpoint).Outcome);
        }

        /// <summary>
        /// Asserts the root holds no temporary write file, whether the write succeeded or failed.
        /// </summary>
        private void AssertNoTemporaryFiles()
        {
            if (!Directory.Exists(_root))
                return;

            string[] leftovers = Directory.GetFiles(_root, "*.tmp", SearchOption.AllDirectories);
            Assert.AreEqual(0, leftovers.Length, "temp-and-rename leaves no temporary file behind.");
        }

        /// <summary>
        /// Reads a file, blocking on the store's asynchronous call.
        /// </summary>
        /// <param name="store">The store to read through.</param>
        /// <param name="fileName">The file to read.</param>
        /// <returns>The read result.</returns>
        private static SaveFileReadResult Read(FileSaveStore store, string fileName)
        {
            return store.ReadAsync(fileName, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Writes a file, blocking on the store's asynchronous call.
        /// </summary>
        /// <param name="store">The store to write through.</param>
        /// <param name="fileName">The file to write.</param>
        /// <param name="text">The text to write.</param>
        /// <returns>The write result.</returns>
        private static SaveFileWriteResult Write(FileSaveStore store, string fileName, string text)
        {
            return store.WriteAsync(fileName, text, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Deletes a file, blocking on the store's asynchronous call.
        /// </summary>
        /// <param name="store">The store to delete through.</param>
        /// <param name="fileName">The file to delete.</param>
        /// <returns>The delete result.</returns>
        private static SaveFileDeleteResult Delete(FileSaveStore store, string fileName)
        {
            return store.DeleteAsync(fileName, CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
