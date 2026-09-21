using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Iterate.Application.Content.Json;
using Iterate.Application.Logging;
using Iterate.Application.Logging.Tests;
using Iterate.Application.Persistence;

namespace Iterate.Application.Persistence.Tests
{
    /// <summary>
    /// Tests <see cref="PersistenceService"/> over a fake store and a real logger factory: the four-way
    /// load outcome, symmetric save and clear outcomes, and exactly one Warning per failure and none at
    /// all for anything that went as expected.
    ///
    /// Two behaviours are contracts rather than conveniences. A file this build cannot read is left on
    /// disk untouched, because a save written by a later version is the player's data and overwriting it
    /// would destroy what it could not understand. And the developer reset deletes the checkpoint before
    /// it clears the flag, so a half-failed reset never leaves a "first launch" pointing at a stale
    /// checkpoint.
    /// </summary>
    public sealed class PersistenceServiceTests
    {
        private FakeSaveFileStore _store;

        private FakeLogSink _sink;

        private PersistenceService _service;

        [SetUp]
        public void SetUp()
        {
            _store = new FakeSaveFileStore();
            _sink = new FakeLogSink();
            _service = new PersistenceService(_store, new GameLoggerFactory(_sink, new LogFilterConfig(LogLevel.Info)));
        }

        [Test]
        public async Task LoadSettings_WhenAbsent_IsAbsentAndFallsBackToTheDefault()
        {
            LoadResult<SettingsState> result = await _service.LoadSettingsAsync(CancellationToken.None);

            Assert.AreEqual(LoadOutcome.Absent, result.Outcome);
            Assert.IsNull(result.Value);
            Assert.AreEqual(SettingsState.Default, result.ValueOr(SettingsState.Default));
            AssertNoWarnings();
        }

        [Test]
        public async Task LoadMeta_WhenAbsent_IsAbsentAndLogsNothing()
        {
            LoadResult<MetaState> result = await _service.LoadMetaAsync(CancellationToken.None);

            Assert.AreEqual(LoadOutcome.Absent, result.Outcome);
            Assert.AreEqual(MetaState.Default, result.ValueOr(MetaState.Default));
            AssertNoWarnings();
        }

        [Test]
        public async Task LoadCheckpoint_WhenAbsent_IsAbsent()
        {
            LoadResult<CheckpointState> result = await _service.LoadCheckpointAsync(CancellationToken.None);

            Assert.AreEqual(LoadOutcome.Absent, result.Outcome);
            AssertNoWarnings();
        }

        [Test]
        public async Task SaveThenLoad_RoundTripsEachFile()
        {
            MetaState meta = MetaState.Default.WithOnboardingComplete();
            CheckpointState checkpoint = CheckpointFixtures.Full();

            await _service.SaveSettingsAsync(SettingsState.Default, CancellationToken.None);
            await _service.SaveMetaAsync(meta, CancellationToken.None);
            await _service.SaveCheckpointAsync(checkpoint, CancellationToken.None);

            Assert.AreEqual(SettingsState.Default, (await _service.LoadSettingsAsync(CancellationToken.None)).Value);
            Assert.AreEqual(meta, (await _service.LoadMetaAsync(CancellationToken.None)).Value);
            LoadResult<CheckpointState> loaded = await _service.LoadCheckpointAsync(CancellationToken.None);
            Assert.AreEqual(LoadOutcome.Loaded, loaded.Outcome);
            Assert.AreEqual(checkpoint.ConsecutiveFailures, loaded.Value.ConsecutiveFailures);
            AssertNoWarnings();
        }

        [Test]
        public async Task SaveMeta_WritesExactlyTheEnvelopedPayloadToTheRightFile()
        {
            MetaState meta = MetaState.Default.WithOnboardingComplete();

            SaveResult result = await _service.SaveMetaAsync(meta, CancellationToken.None);

            Assert.AreEqual(SaveOutcome.Saved, result.Outcome);
            Assert.IsNull(result.Defect);
            CollectionAssert.AreEqual(new[] { SaveFiles.Meta }, _store.Writes);
            Assert.AreEqual(
                VersionedDocument.Write(SaveFiles.FormatVersion, MetaStateCodec.ToJson(meta)),
                _store.Files[SaveFiles.Meta]
            );
            AssertNoWarnings();
        }

        [Test]
        public async Task SaveCheckpoint_WritesToTheCheckpointFile()
        {
            await _service.SaveCheckpointAsync(CheckpointFixtures.Tutorial(), CancellationToken.None);

            CollectionAssert.AreEqual(new[] { SaveFiles.Checkpoint }, _store.Writes);
            StringAssert.StartsWith("{\n  \"formatVersion\": 1", _store.Files[SaveFiles.Checkpoint]);
        }

        [Test]
        public async Task LoadMeta_WhenMalformed_IsUnreadableAndWarnsWithTheDefect()
        {
            _store.Files[SaveFiles.Meta] = "not json";

            LoadResult<MetaState> result = await _service.LoadMetaAsync(CancellationToken.None);

            Assert.AreEqual(LoadOutcome.Unreadable, result.Outcome);
            Assert.IsNull(result.Value);
            Assert.IsNotNull(result.Defect);
            AssertOneWarning("file", SaveFiles.Meta, "defect");
        }

        [Test]
        public async Task LoadMeta_WhenTheCodecRejectsThePayload_IsUnreadable()
        {
            // Valid JSON at the right version, but the flag is missing — the defect comes from the codec
            // rather than the envelope, and must reach the caller just the same.
            _store.Files[SaveFiles.Meta] = VersionedDocument.Write(
                SaveFiles.FormatVersion,
                new JsonObjectBuilder().Build()
            );

            LoadResult<MetaState> result = await _service.LoadMetaAsync(CancellationToken.None);

            Assert.AreEqual(LoadOutcome.Unreadable, result.Outcome);
            StringAssert.Contains("onboardingComplete", result.Defect);
            AssertOneWarning("file", SaveFiles.Meta, "defect");
        }

        [Test]
        public async Task LoadMeta_AtAnUnsupportedVersion_ReportsTheVersionAndWarns()
        {
            _store.Files[SaveFiles.Meta] = "{\n  \"formatVersion\": 2,\n  \"onboardingComplete\": true\n}\n";

            LoadResult<MetaState> result = await _service.LoadMetaAsync(CancellationToken.None);

            Assert.AreEqual(LoadOutcome.UnsupportedVersion, result.Outcome);
            Assert.AreEqual(2, result.FormatVersion);
            Assert.IsNull(result.Value);
            AssertOneWarning("file", SaveFiles.Meta, "version");
        }

        [Test]
        public async Task LoadMeta_AtAnUnsupportedVersion_LeavesTheFileByteIdentical()
        {
            string original = "{\n  \"formatVersion\": 2,\n  \"onboardingComplete\": true\n}\n";
            _store.Files[SaveFiles.Meta] = original;

            await _service.LoadMetaAsync(CancellationToken.None);

            Assert.AreEqual(original, _store.Files[SaveFiles.Meta], "a save this build cannot read is not rewritten.");
            Assert.AreEqual(0, _store.Writes.Count);
        }

        [Test]
        public async Task LoadMeta_WhenTheStoreCannotRead_IsUnreadableAndWarns()
        {
            _store.Files[SaveFiles.Meta] = "irrelevant";
            _store.Unreadable.Add(SaveFiles.Meta);

            LoadResult<MetaState> result = await _service.LoadMetaAsync(CancellationToken.None);

            Assert.AreEqual(LoadOutcome.Unreadable, result.Outcome);
            AssertOneWarning("file", SaveFiles.Meta, "defect");
        }

        [Test]
        public async Task LoadCheckpoint_WhenMalformed_IsUnreadable()
        {
            _store.Files[SaveFiles.Checkpoint] = "{ not json";

            LoadResult<CheckpointState> result = await _service.LoadCheckpointAsync(CancellationToken.None);

            Assert.AreEqual(LoadOutcome.Unreadable, result.Outcome);
            AssertOneWarning("file", SaveFiles.Checkpoint, "defect");
        }

        [Test]
        public async Task LoadSettings_AtAnUnsupportedVersion_IsUnsupported()
        {
            _store.Files[SaveFiles.Settings] = "{\n  \"formatVersion\": 2\n}\n";

            LoadResult<SettingsState> result = await _service.LoadSettingsAsync(CancellationToken.None);

            Assert.AreEqual(LoadOutcome.UnsupportedVersion, result.Outcome);
            Assert.AreEqual(2, result.FormatVersion);
            Assert.AreEqual(SettingsState.Default, result.ValueOr(SettingsState.Default));
        }

        [Test]
        public async Task Save_WhenTheStoreCannotWrite_FailsAndWarns()
        {
            _store.Unwritable.Add(SaveFiles.Meta);

            SaveResult result = await _service.SaveMetaAsync(MetaState.Default, CancellationToken.None);

            Assert.AreEqual(SaveOutcome.Failed, result.Outcome);
            Assert.IsNotNull(result.Defect);
            AssertOneWarning("file", SaveFiles.Meta, "defect");
        }

        [Test]
        public async Task ClearCheckpoint_ClearsThenReportsAbsent()
        {
            await _service.SaveCheckpointAsync(CheckpointFixtures.Tutorial(), CancellationToken.None);

            ClearResult cleared = await _service.ClearCheckpointAsync(CancellationToken.None);
            ClearResult again = await _service.ClearCheckpointAsync(CancellationToken.None);

            Assert.AreEqual(ClearOutcome.Cleared, cleared.Outcome);
            Assert.AreEqual(ClearOutcome.Absent, again.Outcome);
            AssertNoWarnings();
        }

        [Test]
        public async Task ClearCheckpoint_WhenTheDeleteFails_ReportsFailedAndWarns()
        {
            _store.Files[SaveFiles.Checkpoint] = "irrelevant";
            _store.Unwritable.Add(SaveFiles.Checkpoint);

            ClearResult result = await _service.ClearCheckpointAsync(CancellationToken.None);

            Assert.AreEqual(ClearOutcome.Failed, result.Outcome);
            Assert.IsNotNull(result.Defect);
            AssertOneWarning("file", SaveFiles.Checkpoint, "defect");
        }

        [Test]
        public async Task ResetOnboarding_DeletesTheCheckpointBeforeItSavesTheFlag()
        {
            // The order is the whole point: a reset that saved the cleared flag first and then failed to
            // delete would leave a first launch pointing at a checkpoint from a finished onboarding.
            await _service.SaveCheckpointAsync(CheckpointFixtures.Tutorial(), CancellationToken.None);
            await _service.SaveMetaAsync(MetaState.Default.WithOnboardingComplete(), CancellationToken.None);

            ResetOnboardingResult result = await _service.ResetOnboardingAsync(
                MetaState.Default.WithOnboardingComplete(),
                CancellationToken.None
            );

            Assert.AreEqual(ClearOutcome.Cleared, result.Checkpoint.Outcome);
            Assert.AreEqual(SaveOutcome.Saved, result.Meta.Outcome);
            Assert.IsFalse(result.State.OnboardingComplete);
            CollectionAssert.AreEqual(new[] { SaveFiles.Checkpoint }, _store.Deletes);
            Assert.AreEqual(SaveFiles.Meta, _store.Writes[_store.Writes.Count - 1]);
            Assert.IsFalse(_store.Files.ContainsKey(SaveFiles.Checkpoint));
        }

        [Test]
        public async Task ResetOnboarding_SavesTheClearedFlagToDisk()
        {
            await _service.SaveMetaAsync(MetaState.Default.WithOnboardingComplete(), CancellationToken.None);

            await _service.ResetOnboardingAsync(MetaState.Default.WithOnboardingComplete(), CancellationToken.None);

            LoadResult<MetaState> reloaded = await _service.LoadMetaAsync(CancellationToken.None);
            Assert.AreEqual(LoadOutcome.Loaded, reloaded.Outcome);
            Assert.IsFalse(reloaded.Value.OnboardingComplete);
        }

        [Test]
        public async Task ResetOnboarding_WhenTheCheckpointCannotBeDeleted_StillClearsTheFlag()
        {
            _store.Files[SaveFiles.Checkpoint] = "irrelevant";
            _store.Unwritable.Add(SaveFiles.Checkpoint);

            ResetOnboardingResult result = await _service.ResetOnboardingAsync(
                MetaState.Default.WithOnboardingComplete(),
                CancellationToken.None
            );

            Assert.AreEqual(ClearOutcome.Failed, result.Checkpoint.Outcome);
            Assert.AreEqual(SaveOutcome.Saved, result.Meta.Outcome, "a failed delete must not abandon the reset.");
            Assert.IsFalse(result.State.OnboardingComplete);
        }

        [Test]
        public async Task ConcurrentSaves_BothComplete()
        {
            // The service serialises writes behind one semaphore, so two saves started without awaiting
            // must both finish rather than one being lost or throwing.
            Task<SaveResult> first = _service.SaveCheckpointAsync(CheckpointFixtures.Tutorial(), CancellationToken.None);
            Task<SaveResult> second = _service.SaveCheckpointAsync(CheckpointFixtures.Full(), CancellationToken.None);

            SaveResult[] results = await Task.WhenAll(first, second);

            Assert.AreEqual(SaveOutcome.Saved, results[0].Outcome);
            Assert.AreEqual(SaveOutcome.Saved, results[1].Outcome);
            Assert.AreEqual(2, _store.Writes.Count);
        }

        [Test]
        public void ACancelledToken_ReachesTheStore()
        {
            CancellationTokenSource source = new();
            source.Cancel();

            Assert.CatchAsync<System.OperationCanceledException>(
                async () => await _service.LoadMetaAsync(source.Token)
            );
            Assert.CatchAsync<System.OperationCanceledException>(
                async () => await _service.SaveMetaAsync(MetaState.Default, source.Token)
            );
            Assert.CatchAsync<System.OperationCanceledException>(
                async () => await _service.ClearCheckpointAsync(source.Token)
            );
        }

        [Test]
        public void Constructor_RejectsANullStoreOrFactory()
        {
            Assert.Throws<System.ArgumentException>(
                () => _ = new PersistenceService(null, new GameLoggerFactory(_sink, new LogFilterConfig(LogLevel.Info)))
            );
            Assert.Throws<System.ArgumentException>(() => _ = new PersistenceService(_store, null));
        }

        /// <summary>
        /// Asserts the sink recorded nothing at all.
        /// </summary>
        private void AssertNoWarnings()
        {
            Assert.AreEqual(0, _sink.Entries.Count, "an expected outcome is not a log line.");
        }

        /// <summary>
        /// Asserts the sink recorded exactly one Warning under the Persistence category carrying the two
        /// named fields.
        /// </summary>
        /// <param name="firstKey">The first field's key.</param>
        /// <param name="firstValue">The first field's string value.</param>
        /// <param name="secondKey">The second field's key.</param>
        private void AssertOneWarning(string firstKey, string firstValue, string secondKey)
        {
            Assert.AreEqual(1, _sink.Entries.Count, "a failure logs exactly one Warning.");

            LogEntry entry = _sink.Entries[0];
            Assert.AreEqual(LogLevel.Warning, entry.Level);
            Assert.AreEqual(LogCategories.Persistence, entry.Category);
            Assert.AreEqual(2, entry.FieldCount);
            Assert.AreEqual(firstKey, entry.Field0.Key);
            Assert.AreEqual(firstValue, entry.Field0.StringValue);
            Assert.AreEqual(secondKey, entry.Field1.Key);
        }
    }
}
