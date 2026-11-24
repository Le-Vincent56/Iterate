using NUnit.Framework;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests the order of Process entry, which is a contract: Branch confirmation, then the lifecycle's
    /// entry moment, then setup and creation, and only then the Process scope. The capture moment is
    /// observed through the Session's instance identities. The first route Process names a Process
    /// rule, and creating it allocates an instance identity for that rule, so the identity seen at the
    /// entry moment must still be the one from before creation. A configuration naming no rule would
    /// allocate nothing and prove nothing.
    /// </summary>
    public sealed class ProcessEntryOrderTests
    {
        [Test]
        public void Entry_AfterConfirm_AnnouncesEntryBeforeTheProcessAllocatesAnything()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.BranchFirstSystem);
            SessionState session = rig.Scopes.Session;
            rig.Flow.Execute(new ConfigureBranch(BranchAction.Exclude, session.Repository.EntriesOf(SessionFlowFixtures.Instruction)[0].Item.InstanceID));
            int beforeCreate = session.InstanceIDs.LastAllocated;

            rig.Flow.Execute(new ConfigureBranch(BranchAction.Confirm, null));

            Assert.AreEqual(1, rig.Lifecycle.LastAllocatedAtEntry.Count);
            Assert.AreEqual(beforeCreate, rig.Lifecycle.LastAllocatedAtEntry[0], "Entry was announced after the Process allocated.");
            Assert.AreEqual(beforeCreate + 1, session.InstanceIDs.LastAllocated, "The Process rule allocates one instance identity.");
            Assert.IsNotNull(rig.Scopes.Process.ProcessRule);
        }

        [Test]
        public void Entry_AfterConfirm_TheLogShowsConfirmThenEntryThenTheProcessScope()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.BranchFirstSystem);
            SessionState session = rig.Scopes.Session;
            rig.Flow.Execute(new ConfigureBranch(BranchAction.Exclude, session.Repository.EntriesOf(SessionFlowFixtures.Instruction)[0].Item.InstanceID));
            rig.Log.Clear();

            rig.Flow.Execute(new ConfigureBranch(BranchAction.Confirm, null));

            CollectionAssert.AreEqual(
                new[]
                {
                    "entry WB-PROC-003",
                    "open process scope WB-PROC-003",
                    "stage BranchConfiguration -> Build",
                },
                rig.Log.GetRange(0, 3)
            );
            Assert.IsNotNull(session.ActiveBranch.Confirmed, "Confirmation precedes entry.");
        }

        [Test]
        public void Entry_ATutorialWithNoBranch_SkipsBranchConfigurationAndEntersBuild()
        {
            SessionFlowRig rig = new();

            rig.Begin(SessionFlowFixtures.MainSystem);

            Assert.AreEqual(1, rig.StageChanges.Count);
            Assert.AreEqual(SessionStage.Build, rig.StageChanges[0].Current);
            CollectionAssert.AreEqual(
                new[] { "open session scope", "entry WB-PROC-001", "open process scope WB-PROC-001", "stage NoSession -> Build" },
                rig.Log
            );
            Assert.IsNull(rig.Scopes.Session.ActiveBranch.Confirmed);
        }

        [Test]
        public void Entry_BeforeARouteIsCommitted_PassesNoRoute()
        {
            SessionFlowRig rig = new();

            rig.Begin(SessionFlowFixtures.MainSystem);

            Assert.AreEqual(1, rig.Lifecycle.RoutesAtEntry.Count);
            Assert.IsNull(rig.Lifecycle.RoutesAtEntry[0]);
        }

        [Test]
        public void Entry_TheBuildIsOpenedOverTheCreatedProcess()
        {
            SessionFlowRig rig = new();

            rig.Begin(SessionFlowFixtures.MainSystem);

            Assert.IsNotNull(rig.Scopes.Build);
            Assert.AreSame(rig.Scopes.Process.InitialArrangement, rig.Scopes.Build.CurrentArrangement);
        }
    }
}
