using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content.Json;
using Iterate.Application.Logging;
using Iterate.Application.Onboarding;
using Iterate.Application.Persistence;
using Iterate.Application.Persistence.Tests;
using Iterate.Application.SessionFlow;
using Iterate.Application.SessionFlow.Tests;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Onboarding.Tests
{
    /// <summary>
    /// Tests <see cref="OnboardingLifecycle"/>: a checkpoint saved at every entry, a consecutive
    /// failure count that belongs to the checkpoint, recovery by role, completion and abandonment, and
    /// saves that never block play. Saves are queued, so every assertion first waits for the queue.
    /// </summary>
    public sealed class OnboardingLifecycleTests
    {
        private OnboardingRig _rig;
        private SessionState _session;

        [SetUp]
        public void SetUp()
        {
            _rig = new OnboardingRig();
            _session = SessionState.Create(_rig.Catalog, SessionFlowFixtures.Archetype(), "WB-SYS-001", "session-1", "seed-1");
        }

        [Test]
        public void Entry_SavesTheCheckpointForTheProcessWithNoFailures()
        {
            _rig.Onboarding.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, null);

            CheckpointState saved = _rig.SavedCheckpoint();
            Assert.AreEqual(SessionFlowFixtures.TutorialOne, saved.Process);
            Assert.AreEqual(0, saved.ConsecutiveFailures);
            Assert.AreEqual(SaveOutcome.Saved, _rig.Onboarding.LastSaveResult.Outcome);
        }

        [Test]
        public void Failure_IncrementsTheCountAndRewritesTheCheckpoint()
        {
            _rig.Onboarding.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, null);

            _rig.Onboarding.OnProcessFailed(_session, SessionFlowFixtures.TutorialOne, null);

            Assert.AreEqual(1, _rig.Onboarding.ConsecutiveFailures);
            Assert.AreEqual(1, _rig.SavedCheckpoint().ConsecutiveFailures);
        }

        [TestCase("WB-PROC-001", ProcessFailureDisposition.RestartProcess)]
        [TestCase("WB-PROC-002", ProcessFailureDisposition.RestartProcess)]
        [TestCase("WB-PROC-003", ProcessFailureDisposition.ReturnToBranchConfiguration)]
        [TestCase("WB-PROC-005", ProcessFailureDisposition.ReturnToBranchConfiguration)]
        public void Failure_RecoversByTheProcessRole(string process, ProcessFailureDisposition expected)
        {
            Assert.AreEqual(expected, _rig.Onboarding.OnProcessFailed(_session, new ProcessID(process), null));
        }

        [Test]
        public void ReentryOfTheSameProcess_KeepsTheCount()
        {
            _rig.Onboarding.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, null);
            _rig.Onboarding.OnProcessFailed(_session, SessionFlowFixtures.TutorialOne, null);

            _rig.Onboarding.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, null);

            Assert.AreEqual(1, _rig.Onboarding.ConsecutiveFailures);
            Assert.AreEqual(1, _rig.SavedCheckpoint().ConsecutiveFailures);
        }

        [Test]
        public void EntryToAnotherProcess_ResetsTheCount()
        {
            _rig.Onboarding.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, null);
            _rig.Onboarding.OnProcessFailed(_session, SessionFlowFixtures.TutorialOne, null);

            _rig.Onboarding.OnProcessEntry(_session, SessionFlowFixtures.TutorialTwo, null);

            Assert.AreEqual(0, _rig.Onboarding.ConsecutiveFailures);
            Assert.AreEqual(SessionFlowFixtures.TutorialTwo, _rig.SavedCheckpoint().Process);
        }

        [Test]
        public void Resume_ThenEntryToItsProcess_KeepsTheRestoredCount()
        {
            CheckpointState restored = CheckpointCapture.Capture(_session, SessionFlowFixtures.TutorialOne, null, 2).State;

            _rig.Onboarding.Resume(restored);
            _rig.Onboarding.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, null);

            Assert.AreEqual(2, _rig.SavedCheckpoint().ConsecutiveFailures);
        }

        [Test]
        public void Completion_SavesTheFlagAndClearsTheCheckpoint()
        {
            _rig.Onboarding.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, null);

            _rig.Onboarding.OnSessionCompleted(_session);

            Assert.IsTrue(_rig.SavedMeta().OnboardingComplete);
            Assert.IsNull(_rig.SavedCheckpoint());
            Assert.AreEqual(0, _rig.Onboarding.ConsecutiveFailures);
        }

        [Test]
        public void Completion_WhenTheFlagCannotBeSaved_KeepsTheCheckpoint()
        {
            FakeSaveFileStore store = (FakeSaveFileStore)_rig.Store;
            _rig.Onboarding.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, null);
            store.Unwritable.Add(SaveFiles.Meta);

            _rig.Onboarding.OnSessionCompleted(_session);

            Assert.IsFalse(_rig.SavedMeta().OnboardingComplete);
            Assert.IsNotNull(_rig.SavedCheckpoint(), "A failed completion save leaves the last checkpoint to resume from.");
            Assert.AreEqual(SaveOutcome.Failed, _rig.Onboarding.LastSaveResult.Outcome);
        }

        [Test]
        public void Abandonment_ClearsTheCheckpoint()
        {
            _rig.Onboarding.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, null);

            _rig.Onboarding.OnSessionAbandoned(_session);

            Assert.IsNull(_rig.SavedCheckpoint());
            Assert.IsFalse(_rig.SavedMeta().OnboardingComplete);
        }

        [Test]
        public void UnwritableCheckpoint_PlayContinuesAndTheFailureIsRecordedOnce()
        {
            FakeSaveFileStore store = (FakeSaveFileStore)_rig.Store;
            store.Unwritable.Add(SaveFiles.Checkpoint);

            _rig.Onboarding.OnProcessEntry(_session, SessionFlowFixtures.TutorialOne, null);
            _rig.Onboarding.PendingWork.GetAwaiter().GetResult();

            Assert.AreEqual(SaveOutcome.Failed, _rig.Onboarding.LastSaveResult.Outcome);
            Assert.AreEqual(1, _rig.CountAt(LogLevel.Warning));
            Assert.AreEqual(0, _rig.CountAt(LogLevel.Error));
            Assert.DoesNotThrow(() => _rig.Onboarding.OnProcessFailed(_session, SessionFlowFixtures.TutorialOne, null));
        }

        [Test]
        public void QueuedSaves_LandInOrderWithTheValuesCapturedWhenQueued()
        {
            GatedSaveFileStore store = new();
            OnboardingRig rig = new(store);
            SessionState session = SessionState.Create(rig.Catalog, SessionFlowFixtures.Archetype(), "WB-SYS-001", "session-1", "seed-1");
            store.Hold();

            rig.Onboarding.OnProcessEntry(session, SessionFlowFixtures.TutorialOne, null);
            rig.Onboarding.OnProcessFailed(session, SessionFlowFixtures.TutorialOne, null);
            rig.Onboarding.OnProcessEntry(session, SessionFlowFixtures.TutorialOne, null);
            store.Release();
            rig.Onboarding.PendingWork.GetAwaiter().GetResult();

            Assert.AreEqual(3, store.CheckpointWrites.Count);
            int[] expected = { 0, 1, 1 };
            for (int index = 0; index < expected.Length; index++)
            {
                CheckpointState written = Decode(store.CheckpointWrites[index]);
                Assert.AreEqual(SessionFlowFixtures.TutorialOne, written.Process, $"write {index}");
                Assert.AreEqual(expected[index], written.ConsecutiveFailures, $"write {index}");
            }
        }

        /// <summary>
        /// Decodes a written checkpoint document.
        /// </summary>
        /// <param name="text">The document text.</param>
        /// <returns>The checkpoint.</returns>
        private static CheckpointState Decode(string text)
        {
            FakeSaveFileStore single = new();
            single.Files[SaveFiles.Checkpoint] = text;
            PersistenceService reader = new(single, new GameLoggerFactory(new Iterate.Application.Logging.Tests.FakeLogSink(), new LogFilterConfig(LogLevel.Info)));
            return reader.LoadCheckpointAsync(CancellationToken.None).GetAwaiter().GetResult().Value;
        }
    }
}
