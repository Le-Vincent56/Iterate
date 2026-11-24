using System;
using NUnit.Framework;
using Iterate.Application.Persistence;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests <see cref="SessionFlow.BeginSession(SessionState, SystemID, CheckpointDestination)"/>: a
    /// restored Session resumes at its destination's Process, with the route that led there committed,
    /// and re-enters by the destination's recovery. A scripted restart enters the Process directly; a
    /// return to Branch configuration opens the draft seeded from the restored confirmed Branch.
    /// </summary>
    public sealed class RestoredBeginTests
    {
        private ContentCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _catalog = SessionFlowFixtures.Catalog();
        }

        [Test]
        public void Begin_ScriptedRestart_EntersTheProcessInBuild()
        {
            SessionFlowRig rig = new();
            SessionState restored = FreshSession();

            SessionBeginResult result = rig.Flow.BeginSession(
                restored,
                SessionFlowFixtures.MainSystem,
                new CheckpointDestination(SessionFlowFixtures.TutorialOne, null, CheckpointRecovery.ScriptedRestart)
            );

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(SessionStage.Build, rig.Stage);
            Assert.AreSame(restored, rig.Scopes.Session);
            Assert.AreEqual(SessionFlowFixtures.TutorialOne.Value, rig.Scopes.Process.ProcessIdentity);
            Assert.AreEqual(new StageChange(SessionStage.NoSession, SessionStage.Build, SessionEndReason.None), rig.StageChanges[0]);
        }

        [Test]
        public void Begin_BranchConfiguration_OpensTheDraftSeededFromTheConfirmedBranchWithTheRouteCommitted()
        {
            SessionFlowRig rig = new();
            SessionState restored = SessionWithConfirmedBranch();
            ActiveBranchConfiguration confirmed = restored.ActiveBranch.Confirmed;

            SessionBeginResult result = rig.Flow.BeginSession(
                restored,
                SessionFlowFixtures.MainSystem,
                new CheckpointDestination(SessionFlowFixtures.RouteOneProcess, SessionFlowFixtures.RouteOne, CheckpointRecovery.BranchConfiguration)
            );

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(SessionStage.BranchConfiguration, rig.Stage);
            Assert.IsTrue(restored.ActiveBranch.HasConfigurationInProgress);
            foreach (InstanceID eligible in confirmed.Eligible)
                Assert.IsTrue(restored.ActiveBranch.StateOf(eligible).HasFlag(BranchInstanceState.Selected), eligible.ToString());

            rig.Flow.Execute(new ConfigureBranch(BranchAction.Confirm, null));
            Assert.AreEqual(SessionStage.Build, rig.Stage);
            Assert.AreEqual(SessionFlowFixtures.RouteOne, rig.Lifecycle.RoutesAtEntry[0], "The restored route is committed.");
        }

        [Test]
        public void Begin_TheCriticalProcessAfterARoute_KeepsTheRouteCommitted()
        {
            SessionFlowRig rig = new();

            SessionBeginResult result = rig.Flow.BeginSession(
                SessionWithConfirmedBranch(),
                SessionFlowFixtures.MainSystem,
                new CheckpointDestination(SessionFlowFixtures.CriticalProcess, SessionFlowFixtures.RouteTwo, CheckpointRecovery.BranchConfiguration)
            );
            rig.Flow.Execute(new ConfigureBranch(BranchAction.Confirm, null));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(SessionFlowFixtures.CriticalProcess.Value, rig.Scopes.Process.ProcessIdentity);
            Assert.AreEqual(SessionFlowFixtures.RouteTwo, rig.Lifecycle.RoutesAtEntry[0]);
        }

        [Test]
        public void Begin_AProcessTheSystemDoesNotHold_IsUnknownProcessAndOpensNothing()
        {
            SessionFlowRig rig = new();

            SessionBeginResult result = rig.Flow.BeginSession(
                FreshSession(),
                SessionFlowFixtures.MainSystem,
                new CheckpointDestination(SessionFlowFixtures.MissingCoreProcess, null, CheckpointRecovery.ScriptedRestart)
            );

            Assert.AreEqual(SessionBeginRejection.UnknownProcess, result.Rejection);
            Assert.AreEqual(0, rig.Scopes.SessionScopesOpened);
        }

        [Test]
        public void Begin_ARouteProcessWithoutItsRoute_IsUnknownProcess()
        {
            SessionFlowRig rig = new();

            SessionBeginResult result = rig.Flow.BeginSession(
                FreshSession(),
                SessionFlowFixtures.MainSystem,
                new CheckpointDestination(SessionFlowFixtures.RouteOneProcess, null, CheckpointRecovery.BranchConfiguration)
            );

            Assert.AreEqual(SessionBeginRejection.UnknownProcess, result.Rejection);
        }

        [Test]
        public void Begin_ARouteTheSystemDoesNotOffer_IsUnknownRouteAndOpensNothing()
        {
            SessionFlowRig rig = new();

            SessionBeginResult result = rig.Flow.BeginSession(
                FreshSession(),
                SessionFlowFixtures.MainSystem,
                new CheckpointDestination(SessionFlowFixtures.CriticalProcess, new RouteID("WB-ROUTE-999"), CheckpointRecovery.BranchConfiguration)
            );

            Assert.AreEqual(SessionBeginRejection.UnknownRoute, result.Rejection);
            Assert.AreEqual(0, rig.Scopes.SessionScopesOpened);
        }

        [Test]
        public void Begin_WhileASessionIsActive_IsSessionAlreadyActive()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.MainSystem);

            SessionBeginResult result = rig.Flow.BeginSession(
                FreshSession(),
                SessionFlowFixtures.MainSystem,
                new CheckpointDestination(SessionFlowFixtures.TutorialOne, null, CheckpointRecovery.ScriptedRestart)
            );

            Assert.AreEqual(SessionBeginRejection.SessionAlreadyActive, result.Rejection);
        }

        [Test]
        public void Begin_NullSessionOrDestination_ThrowsArgumentNullException()
        {
            SessionFlowRig rig = new();
            CheckpointDestination destination = new(SessionFlowFixtures.TutorialOne, null, CheckpointRecovery.ScriptedRestart);

            Assert.Throws<ArgumentNullException>(() => rig.Flow.BeginSession(null, SessionFlowFixtures.MainSystem, destination));
            Assert.Throws<ArgumentNullException>(() => rig.Flow.BeginSession(FreshSession(), SessionFlowFixtures.MainSystem, null));
        }

        /// <summary>
        /// A Session as a checkpoint before any Branch confirmation would restore it.
        /// </summary>
        /// <returns>The Session.</returns>
        private SessionState FreshSession()
        {
            return SessionState.Create(_catalog, SessionFlowFixtures.Archetype(), SessionFlowFixtures.MainSystem.Value, "session-1", "seed-1");
        }

        /// <summary>
        /// A Session whose Branch was confirmed with the Structure and one Instruction, as a checkpoint
        /// at a route Process would restore it.
        /// </summary>
        /// <returns>The Session.</returns>
        private SessionState SessionWithConfirmedBranch()
        {
            SessionState session = FreshSession();
            ActiveBranchSpec spec = SessionFlowFixtures.Branch();
            session.ActiveBranch.BeginConfiguration(
                new BranchConstraints(spec.Capacity, spec.Required, spec.Quarantined, spec.RecommendedTags, spec.CautionTags),
                session.Repository.Snapshot()
            );
            session.ActiveBranch.Exclude(session.Repository.EntriesOf(SessionFlowFixtures.Instruction)[0].Item.InstanceID);
            Assert.IsTrue(session.ActiveBranch.Confirm().Succeeded);
            return session;
        }
    }
}
