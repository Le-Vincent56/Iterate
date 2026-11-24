using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Iterate.Application.Onboarding;
using Iterate.Application.Persistence;
using Iterate.Application.Persistence.Tests;
using Iterate.Application.SessionFlow;
using Iterate.Application.SessionFlow.Tests;
using Iterate.Domain.Progression;

namespace Iterate.Application.Onboarding.Tests
{
    /// <summary>
    /// Tests <see cref="LaunchService"/>. Until onboarding is complete a launch enters onboarding,
    /// resuming at a checkpoint that restores and clearing one that does not; afterwards it begins an
    /// ordinary Session with fresh identities. A launch waits for saves in flight, and neither a begin
    /// nor a developer reset touches anything while a Session runs.
    /// </summary>
    public sealed class LaunchServiceTests
    {
        [Test]
        public void FirstLaunch_IsOnboardingFromTheStart()
        {
            OnboardingRig rig = new();

            LaunchDecision decision = rig.Launch.DecideAsync(CancellationToken.None).GetAwaiter().GetResult();
            SessionBeginResult begun = rig.Begin();

            Assert.AreEqual(new LaunchDecision(SessionMode.Onboarding, false), decision);
            Assert.IsTrue(begun.Succeeded);
            Assert.AreEqual(SessionMode.Onboarding, rig.Router.Mode);
            Assert.AreEqual(SessionStage.Build, rig.Queries.Stage);
            Assert.AreEqual(SessionFlowFixtures.TutorialOne.Value, rig.Scopes.Process.ProcessIdentity);
            Assert.AreEqual("onboarding", rig.Scopes.Session.SessionIdentity);
            Assert.AreEqual(0, rig.Identities.Issued);
        }

        [Test]
        public void StoredTutorialCheckpoint_ResumesInBuildWithItsCount()
        {
            FakeSaveFileStore store = new();
            OnboardingRig first = new(store);
            first.Begin();
            first.Onboarding.OnProcessFailed(first.Scopes.Session, SessionFlowFixtures.TutorialOne, null);
            first.SavedCheckpoint();

            OnboardingRig second = new(store);
            LaunchDecision decision = second.Launch.DecideAsync(CancellationToken.None).GetAwaiter().GetResult();
            SessionBeginResult begun = second.Begin();

            Assert.IsTrue(decision.ResumesAtCheckpoint);
            Assert.IsTrue(begun.Succeeded);
            Assert.AreEqual(SessionStage.Build, second.Queries.Stage);
            Assert.AreEqual(1, second.Onboarding.ConsecutiveFailures);
        }

        [Test]
        public void StoredRouteCheckpoint_ResumesInBranchConfiguration()
        {
            FakeSaveFileStore store = new();
            OnboardingRig first = new(store);
            SessionState session = SessionState.Create(first.Catalog, SessionFlowFixtures.Archetype(), "WB-SYS-001", "onboarding", "onboarding-seed");
            ConfirmBranch(session);
            first.Onboarding.OnProcessEntry(session, SessionFlowFixtures.RouteOneProcess, SessionFlowFixtures.RouteOne);
            first.SavedCheckpoint();

            OnboardingRig second = new(store);
            SessionBeginResult begun = second.Begin();

            Assert.IsTrue(begun.Succeeded);
            Assert.AreEqual(SessionStage.BranchConfiguration, second.Queries.Stage);
        }

        [Test]
        public void CheckpointFromAnotherRevision_IsClearedAndOnboardingStartsFresh()
        {
            FakeSaveFileStore store = new();
            OnboardingRig first = new(store);
            SessionState session = SessionState.Create(first.Catalog, SessionFlowFixtures.Archetype(), "WB-SYS-001", "onboarding", "onboarding-seed");
            CheckpointState stale = CheckpointCapture.Capture(session, SessionFlowFixtures.TutorialOne, null, 0).State with { CatalogRevision = "older-revision" };
            first.Persistence.SaveCheckpointAsync(stale, CancellationToken.None).GetAwaiter().GetResult();

            OnboardingRig second = new(store);
            LaunchDecision decision = second.Launch.DecideAsync(CancellationToken.None).GetAwaiter().GetResult();
            SessionBeginResult begun = second.Begin();

            Assert.IsFalse(decision.ResumesAtCheckpoint);
            Assert.IsTrue(begun.Succeeded);
            Assert.AreEqual(0, second.Onboarding.ConsecutiveFailures);
            Assert.AreEqual(SessionFlowFixtures.TutorialOne, second.SavedCheckpoint().Process, "The stale file was replaced by the fresh entry's checkpoint.");
            Assert.AreEqual(session.CatalogRevision, second.SavedCheckpoint().CatalogRevision);
        }

        [Test]
        public void UnreadableCheckpoint_IsClearedAndOnboardingStartsFresh()
        {
            FakeSaveFileStore store = new();
            store.Files[SaveFiles.Checkpoint] = "{ not json";
            OnboardingRig rig = new(store);

            SessionBeginResult begun = rig.Begin();

            Assert.IsTrue(begun.Succeeded);
            CollectionAssert.Contains(store.Deletes, SaveFiles.Checkpoint);
            Assert.AreEqual(SessionStage.Build, rig.Queries.Stage);
        }

        [Test]
        public void CompletedOnboarding_BeginsAnOrdinarySessionWithFreshIdentities()
        {
            FakeSaveFileStore store = new();
            OnboardingRig first = new(store);
            first.Persistence.SaveMetaAsync(MetaState.Default.WithOnboardingComplete(), CancellationToken.None).GetAwaiter().GetResult();

            OnboardingRig second = new(store);
            LaunchDecision decision = second.Launch.DecideAsync(CancellationToken.None).GetAwaiter().GetResult();
            second.Begin();

            Assert.AreEqual(new LaunchDecision(SessionMode.Ordinary, false), decision);
            Assert.AreEqual(SessionMode.Ordinary, second.Router.Mode);
            Assert.AreEqual("ordinary-1", second.Scopes.Session.SessionIdentity);
            Assert.AreEqual("ordinary-seed-1", second.Scopes.Session.SessionSeedIdentity);
            Assert.IsNull(second.SavedCheckpoint(), "An ordinary Session saves no checkpoint.");
        }

        [Test]
        public void Decide_WaitsForASaveStillInFlight()
        {
            GatedSaveFileStore store = new();
            OnboardingRig rig = new(store);
            SessionState session = SessionState.Create(rig.Catalog, SessionFlowFixtures.Archetype(), "WB-SYS-001", "onboarding", "onboarding-seed");
            store.Hold();
            rig.Onboarding.OnProcessEntry(session, SessionFlowFixtures.TutorialOne, null);

            // LaunchService keeps the caller's context, and the Editor runs tests on its main thread, so
            // blocking on a continuation posted to that context would deadlock. The decision runs with no
            // context so the released save resumes it on the pool.
            SynchronizationContext callerContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                Task<LaunchDecision> deciding = rig.Launch.DecideAsync(CancellationToken.None);
                Assert.IsFalse(deciding.IsCompleted, "The decision must not read the files while a save is held.");
                store.Release();

                Assert.IsTrue(deciding.GetAwaiter().GetResult().ResumesAtCheckpoint);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(callerContext);
            }
        }

        [Test]
        public void Reset_DuringASession_IsRefusedAndTouchesNoFile()
        {
            FakeSaveFileStore store = new();
            OnboardingRig rig = new(store);
            rig.Begin();
            rig.SavedCheckpoint();
            int deletes = store.Deletes.Count;
            int writes = store.Writes.Count;

            OnboardingResetResult result = rig.Launch.ResetOnboardingAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(result.Refused);
            Assert.IsNull(result.Reset);
            Assert.AreEqual(deletes, store.Deletes.Count);
            Assert.AreEqual(writes, store.Writes.Count);
        }

        [Test]
        public void Reset_AfterCompletion_MakesTheNextLaunchOnboarding()
        {
            FakeSaveFileStore store = new();
            OnboardingRig rig = new(store);
            rig.Persistence.SaveMetaAsync(MetaState.Default.WithOnboardingComplete(), CancellationToken.None).GetAwaiter().GetResult();

            OnboardingResetResult result = rig.Launch.ResetOnboardingAsync(CancellationToken.None).GetAwaiter().GetResult();
            LaunchDecision decision = new OnboardingRig(store).Launch.DecideAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsFalse(result.Refused);
            Assert.AreEqual(SessionMode.Onboarding, decision.Mode);
        }

        [Test]
        public void Begin_DuringAnOnboardingSession_IsRefusedAndChangesNothing()
        {
            FakeSaveFileStore store = new();
            OnboardingRig rig = new(store);
            rig.Begin();
            rig.Onboarding.OnProcessFailed(rig.Scopes.Session, SessionFlowFixtures.TutorialOne, null);
            rig.SavedCheckpoint();
            int deletes = store.Deletes.Count;

            SessionBeginResult again = rig.Begin();

            Assert.AreEqual(SessionBeginRejection.SessionAlreadyActive, again.Rejection);
            Assert.AreEqual(SessionMode.Onboarding, rig.Router.Mode);
            Assert.AreEqual(1, rig.Onboarding.ConsecutiveFailures);
            Assert.AreEqual(deletes, store.Deletes.Count);
            Assert.IsNotNull(rig.SavedCheckpoint());
        }

        /// <summary>
        /// Confirms a Branch on a Session the way Branch configuration would, excluding one Instruction
        /// to fit the route Process's capacity.
        /// </summary>
        /// <param name="session">The Session.</param>
        private static void ConfirmBranch(SessionState session)
        {
            Iterate.Domain.Content.ActiveBranchSpec spec = SessionFlowFixtures.Branch();
            session.ActiveBranch.BeginConfiguration(
                new BranchConstraints(spec.Capacity, spec.Required, spec.Quarantined, spec.RecommendedTags, spec.CautionTags),
                session.Repository.Snapshot()
            );
            session.ActiveBranch.Exclude(session.Repository.EntriesOf(SessionFlowFixtures.Instruction)[0].Item.InstanceID);
            Assert.IsTrue(session.ActiveBranch.Confirm().Succeeded);
        }
    }
}
