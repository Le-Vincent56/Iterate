using System;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests a fresh <see cref="SessionFlow.BeginSession(Iterate.Domain.Content.StarterArchetypeDefinition, SystemID, string, string)"/>:
    /// the refusals, and on success one Session scope, one stage change out of <see cref="SessionStage.NoSession"/>,
    /// and the System's first step entered. A Session may begin again once the last one has ended.
    /// </summary>
    public sealed class SessionBeginTests
    {
        [Test]
        public void Begin_CatalogNotLoaded_IsCatalogUnavailableAndOpensNothing()
        {
            SessionFlowRig rig = new(new CatalogHolder(), 0, 1);

            SessionBeginResult result = rig.Begin(SessionFlowFixtures.MainSystem);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(SessionBeginRejection.CatalogUnavailable, result.Rejection);
            Assert.AreEqual(0, rig.Scopes.SessionScopesOpened);
            CollectionAssert.IsEmpty(rig.StageChanges);
        }

        [Test]
        public void Begin_UnknownSystem_IsUnknownSystemAndOpensNothing()
        {
            SessionFlowRig rig = new();

            SessionBeginResult result = rig.Begin(new SystemID("WB-SYS-999"));

            Assert.AreEqual(SessionBeginRejection.UnknownSystem, result.Rejection);
            Assert.AreEqual(0, rig.Scopes.SessionScopesOpened);
            CollectionAssert.IsEmpty(rig.StageChanges);
        }

        [Test]
        public void Begin_NullArchetype_ThrowsArgumentNullException()
        {
            SessionFlowRig rig = new();

            Assert.Throws<ArgumentNullException>(() => rig.Flow.BeginSession(null, SessionFlowFixtures.MainSystem, "session-1", "seed-1"));
        }

        [Test]
        public void Begin_MainSystem_OpensOneSessionScopeAndEntersTheFirstTutorialInBuild()
        {
            SessionFlowRig rig = new();

            SessionBeginResult result = rig.Begin(SessionFlowFixtures.MainSystem);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(SessionBeginRejection.None, result.Rejection);
            Assert.AreEqual(1, rig.Scopes.SessionScopesOpened);
            Assert.AreEqual(1, rig.StageChanges.Count);
            Assert.AreEqual(new StageChange(SessionStage.NoSession, SessionStage.Build, SessionEndReason.None), rig.StageChanges[0]);
            Assert.AreEqual(SessionFlowFixtures.TutorialOne.Value, rig.Scopes.Process.ProcessIdentity);
        }

        [Test]
        public void Begin_ShopFirstSystem_OpensTheShopTargetingTheProcessAfterIt()
        {
            SessionFlowRig rig = new();

            rig.Begin(SessionFlowFixtures.ShopFirstSystem);

            Assert.AreEqual(SessionStage.Shop, rig.Stage);
            Assert.AreEqual(0, rig.Scopes.ProcessScopesOpened, "A shop opens no Process scope.");
        }

        [Test]
        public void Begin_RouteFirstSystem_StandsOnTheRouteChoice()
        {
            SessionFlowRig rig = new();

            rig.Begin(SessionFlowFixtures.RouteFirstSystem);

            Assert.AreEqual(SessionStage.RouteSelection, rig.Stage);
        }

        [Test]
        public void Begin_BranchFirstSystem_StandsInBranchConfigurationWithADraftOpen()
        {
            SessionFlowRig rig = new();

            rig.Begin(SessionFlowFixtures.BranchFirstSystem);

            Assert.AreEqual(SessionStage.BranchConfiguration, rig.Stage);
            Assert.IsTrue(rig.Scopes.Session.ActiveBranch.HasConfigurationInProgress);
            CollectionAssert.IsEmpty(rig.Lifecycle.LastAllocatedAtEntry, "Branch configuration precedes Process entry.");
        }

        [Test]
        public void Begin_WhileASessionIsActive_IsSessionAlreadyActiveAndChangesNothing()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.MainSystem);

            SessionBeginResult again = rig.Begin(SessionFlowFixtures.RouteFirstSystem);

            Assert.AreEqual(SessionBeginRejection.SessionAlreadyActive, again.Rejection);
            Assert.AreEqual(1, rig.Scopes.SessionScopesOpened);
            Assert.AreEqual(SessionStage.Build, rig.Stage);
        }

        [Test]
        public void Begin_AfterTheSessionEnded_BeginsAFreshSession()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.MainSystem);
            rig.Flow.Execute(new AbandonSession());

            SessionBeginResult again = rig.Begin(SessionFlowFixtures.RouteFirstSystem);

            Assert.IsTrue(again.Succeeded);
            Assert.AreEqual(2, rig.Scopes.SessionScopesOpened);
            Assert.AreEqual(new StageChange(SessionStage.SessionEnded, SessionStage.RouteSelection, SessionEndReason.None), rig.StageChanges[rig.StageChanges.Count - 1]);
        }
    }
}
