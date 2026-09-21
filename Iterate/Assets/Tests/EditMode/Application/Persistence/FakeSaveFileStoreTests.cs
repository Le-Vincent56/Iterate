using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Iterate.Application.Persistence;

namespace Iterate.Application.Persistence.Tests
{
    /// <summary>
    /// Tests the <see cref="FakeSaveFileStore"/> double itself and the port's outcome records. The
    /// double is what PER-06's service suite asserts against, so proving it honours its own failure sets
    /// and records writes and deletes in order means a later failure there is the service's, not the
    /// fake's. Only cancellation throws — every other failure is an outcome, which is the port's contract.
    /// </summary>
    public sealed class FakeSaveFileStoreTests
    {
        [Test]
        public async Task Read_OfAnAbsentFile_IsAbsent()
        {
            FakeSaveFileStore store = new();

            SaveFileReadResult result = await store.ReadAsync(SaveFiles.Meta, CancellationToken.None);

            Assert.AreEqual(SaveFileReadOutcome.Absent, result.Outcome);
            Assert.IsNull(result.Text);
            Assert.IsNull(result.Defect);
        }

        [Test]
        public async Task Read_OfAWrittenFile_IsFoundWithItsText()
        {
            FakeSaveFileStore store = new();
            await store.WriteAsync(SaveFiles.Meta, "text", CancellationToken.None);

            SaveFileReadResult result = await store.ReadAsync(SaveFiles.Meta, CancellationToken.None);

            Assert.AreEqual(SaveFileReadOutcome.Found, result.Outcome);
            Assert.AreEqual("text", result.Text);
        }

        [Test]
        public async Task Read_OfAnUnreadableFile_IsUnreadableWithADefect()
        {
            FakeSaveFileStore store = new();
            store.Files[SaveFiles.Meta] = "text";
            store.Unreadable.Add(SaveFiles.Meta);

            SaveFileReadResult result = await store.ReadAsync(SaveFiles.Meta, CancellationToken.None);

            Assert.AreEqual(SaveFileReadOutcome.Unreadable, result.Outcome);
            Assert.IsNotNull(result.Defect);
            Assert.IsNull(result.Text);
        }

        [Test]
        public async Task Write_ToAnUnwritableFile_FailsWithoutStoringTheText()
        {
            FakeSaveFileStore store = new();
            store.Unwritable.Add(SaveFiles.Meta);

            SaveFileWriteResult result = await store.WriteAsync(SaveFiles.Meta, "text", CancellationToken.None);

            Assert.IsFalse(result.Succeeded);
            Assert.IsNotNull(result.Defect);
            Assert.IsFalse(store.Files.ContainsKey(SaveFiles.Meta));
        }

        [Test]
        public async Task Delete_RemovesTheFileThenReportsItAbsent()
        {
            FakeSaveFileStore store = new();
            await store.WriteAsync(SaveFiles.Checkpoint, "text", CancellationToken.None);

            SaveFileDeleteResult first = await store.DeleteAsync(SaveFiles.Checkpoint, CancellationToken.None);
            SaveFileDeleteResult second = await store.DeleteAsync(SaveFiles.Checkpoint, CancellationToken.None);

            Assert.AreEqual(SaveFileDeleteOutcome.Deleted, first.Outcome);
            Assert.AreEqual(SaveFileDeleteOutcome.Absent, second.Outcome);
        }

        [Test]
        public async Task Delete_OfAnUnwritableFile_Fails()
        {
            FakeSaveFileStore store = new();
            store.Files[SaveFiles.Checkpoint] = "text";
            store.Unwritable.Add(SaveFiles.Checkpoint);

            SaveFileDeleteResult result = await store.DeleteAsync(SaveFiles.Checkpoint, CancellationToken.None);

            Assert.AreEqual(SaveFileDeleteOutcome.Failed, result.Outcome);
            Assert.IsTrue(store.Files.ContainsKey(SaveFiles.Checkpoint));
        }

        [Test]
        public async Task WritesAndDeletes_AreRecordedInOrder()
        {
            FakeSaveFileStore store = new();

            await store.WriteAsync(SaveFiles.Meta, "a", CancellationToken.None);
            await store.DeleteAsync(SaveFiles.Checkpoint, CancellationToken.None);
            await store.WriteAsync(SaveFiles.Settings, "b", CancellationToken.None);

            CollectionAssert.AreEqual(new[] { SaveFiles.Meta, SaveFiles.Settings }, store.Writes);
            CollectionAssert.AreEqual(new[] { SaveFiles.Checkpoint }, store.Deletes);
        }

        [Test]
        public void ACancelledToken_ThrowsOnEveryOperation()
        {
            // CatchAsync, not ThrowsAsync: the throw is OperationCanceledException, but an async method
            // that throws it with a matching token completes its task as *cancelled* rather than faulted,
            // and awaiting a cancelled task surfaces the derived TaskCanceledException. The contract is
            // that cancellation propagates, so the assertion is on the category rather than on which of
            // the two the async plumbing happens to hand back.
            FakeSaveFileStore store = new();
            CancellationTokenSource source = new();
            source.Cancel();

            Assert.CatchAsync<System.OperationCanceledException>(
                async () => await store.ReadAsync(SaveFiles.Meta, source.Token)
            );
            Assert.CatchAsync<System.OperationCanceledException>(
                async () => await store.WriteAsync(SaveFiles.Meta, "text", source.Token)
            );
            Assert.CatchAsync<System.OperationCanceledException>(
                async () => await store.DeleteAsync(SaveFiles.Meta, source.Token)
            );
        }
    }
}
