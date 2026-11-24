using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Onboarding;
using Iterate.Application.Persistence.Tests;
using Iterate.Application.SessionFlow;
using Iterate.Application.SessionFlow.Tests;
using Iterate.Domain.Compilation;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Onboarding.Tests
{
    /// <summary>
    /// Plays onboarding end to end through the launch service and Session Flow, quitting and
    /// relaunching by building a new launch over the same save store. A failure keeps the Session's
    /// acquisitions and counts against its checkpoint, a relaunch resumes there, completion records
    /// onboarding as done, and an abandoned onboarding starts over.
    /// </summary>
    public sealed class OnboardingEndToEndTests
    {
        [Test]
        public void FailedTutorial_RestartsWithAcquisitionsKeptAndTheCountSaved()
        {
            OnboardingRig rig = new();
            rig.Begin();
            SessionState session = rig.Scopes.Session;
            List<InstanceID> before = Identities(session);
            ProcessState failed = rig.Scopes.Process;

            CommandResult result = FailTheProcess(rig);

            Assert.AreEqual(SessionStage.Build, result.Stage);
            Assert.AreNotSame(failed, rig.Scopes.Process, "The tutorial restarted as a fresh Process.");
            CollectionAssert.AreEqual(before, Identities(session));
            Assert.AreEqual(1, rig.SavedCheckpoint().ConsecutiveFailures);
        }

        [Test]
        public void QuitAfterAFailure_RelaunchResumesTheTutorialWithItsCount()
        {
            FakeSaveFileStore store = new();
            OnboardingRig first = new(store);
            first.Begin();
            FailTheProcess(first);
            first.SavedCheckpoint();

            OnboardingRig second = new(store);
            SessionBeginResult begun = second.Begin();

            Assert.IsTrue(begun.Succeeded);
            Assert.AreEqual(SessionStage.Build, second.Queries.Stage);
            Assert.AreEqual(SessionFlowFixtures.TutorialOne.Value, second.Scopes.Process.ProcessIdentity);
            Assert.AreEqual(1, second.Onboarding.ConsecutiveFailures);
        }

        [Test]
        public void CompletedOnboarding_RecordsTheFlagLeavesNoCheckpointAndTheNextLaunchIsOrdinary()
        {
            FakeSaveFileStore store = new();
            OnboardingRig rig = new(store, SessionFlowPlayContent.PatchSystem);
            rig.Begin();

            InstanceID overwrite = rig.Scopes.Session.Repository.EntriesOf(SessionFlowPlayContent.OverwriteInstruction)[0].Item.InstanceID;
            Assert.IsTrue(rig.Flow.Execute(new InstallLine(overwrite, new SourcePosition(2))).Accepted);
            Assert.AreEqual(SessionStage.ResultReview, rig.Flow.Execute(new CompileAndExecute()).Stage);
            CommandResult submitted = rig.Flow.Execute(new Submit());

            Assert.AreEqual(SessionStage.SessionEnded, submitted.Stage);
            Assert.IsTrue(rig.SavedMeta().OnboardingComplete);
            Assert.IsNull(rig.SavedCheckpoint());
            LaunchDecision next = new OnboardingRig(store, SessionFlowPlayContent.PatchSystem).Launch.DecideAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(SessionMode.Ordinary, next.Mode);
        }

        [Test]
        public void AbandonedOnboarding_RelaunchesFromTheStartWithNoCount()
        {
            FakeSaveFileStore store = new();
            OnboardingRig first = new(store);
            first.Begin();
            FailTheProcess(first);
            first.Flow.Execute(new AbandonSession());
            Assert.IsNull(first.SavedCheckpoint());

            OnboardingRig second = new(store);
            LaunchDecision decision = second.Launch.DecideAsync(CancellationToken.None).GetAwaiter().GetResult();
            second.Begin();

            Assert.AreEqual(new LaunchDecision(SessionMode.Onboarding, false), decision);
            Assert.AreEqual(SessionFlowFixtures.TutorialOne.Value, second.Scopes.Process.ProcessIdentity);
            Assert.AreEqual(0, second.Onboarding.ConsecutiveFailures);
        }

        /// <summary>
        /// Runs every execution the Process allows on its current source, returning to Build between
        /// them, so the last misses Pass and the Process fails.
        /// </summary>
        /// <param name="rig">The rig.</param>
        /// <returns>The final execution's result.</returns>
        private static CommandResult FailTheProcess(OnboardingRig rig)
        {
            ProcessState process = rig.Scopes.Process;
            while (process.Counters.ExecutionsRemaining > 1)
            {
                Assert.IsTrue(rig.Flow.Execute(new CompileAndExecute()).Accepted);
                Assert.IsTrue(rig.Flow.Execute(new ReturnToBuild()).Accepted);
            }

            return rig.Flow.Execute(new CompileAndExecute());
        }

        /// <summary>
        /// The Session's Repository identities, in order.
        /// </summary>
        /// <param name="session">The Session.</param>
        /// <returns>The identities.</returns>
        private static List<InstanceID> Identities(SessionState session)
        {
            List<InstanceID> identities = new();
            foreach (RepositoryEntry entry in session.Repository.Entries)
                identities.Add(entry.Item.InstanceID);

            return identities;
        }
    }
}
