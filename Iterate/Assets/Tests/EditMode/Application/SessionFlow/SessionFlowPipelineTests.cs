using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Compilation;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests the command pipeline of <see cref="SessionFlow"/>: no Session, the wrong stage, a gate
    /// and a refused Domain operation each stop a command before anything changes, and every command,
    /// accepted or refused, reaches the observers after the events. A command issued from inside an
    /// event is refused outright rather than observed ahead of the command that caused it.
    ///
    /// Holders are compared before and after through the Session the scope factory was handed, which
    /// is the same object the surface writes.
    /// </summary>
    public sealed class SessionFlowPipelineTests
    {
        [Test]
        public void Execute_NullCommand_ThrowsArgumentNullException()
        {
            SessionFlowRig rig = new();

            Assert.Throws<ArgumentNullException>(() => rig.Flow.Execute(null));
        }

        [Test]
        public void Execute_BeforeAnySession_EveryCommandIsNoSessionAndObserved()
        {
            SessionFlowRig rig = new();

            foreach (ISessionCommand command in OneOfEach())
            {
                CommandResult result = rig.Flow.Execute(command);

                Assert.IsFalse(result.Accepted, command.GetType().Name);
                Assert.AreEqual(CommandRejection.NoSession, result.Rejection, command.GetType().Name);
                Assert.AreEqual(SessionStage.NoSession, result.Stage, command.GetType().Name);
            }

            Assert.AreEqual(OneOfEach().Count, rig.Observers[0].Results.Count);
            CollectionAssert.IsEmpty(rig.StageChanges);
            CollectionAssert.IsEmpty(rig.StateChanges);
        }

        [Test]
        public void Execute_AfterTheSessionEnded_IsNoSession()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.MainSystem);
            rig.Flow.Execute(new AbandonSession());

            CommandResult result = rig.Flow.Execute(new CompileAndExecute());

            Assert.AreEqual(CommandRejection.NoSession, result.Rejection);
            Assert.AreEqual(SessionStage.SessionEnded, result.Stage);
        }

        [Test]
        public void Execute_WrongStage_ChangesNothingRaisesNothingAndIsObserved()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.MainSystem);
            Snapshot before = Snapshot.Of(rig);
            int stageEvents = rig.StageChanges.Count;

            CommandResult result = rig.Flow.Execute(new RerollShop());

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(CommandRejection.WrongStage, result.Rejection);
            Assert.IsNull(result.DomainResult);
            Assert.AreEqual(SessionStage.Build, result.Stage);
            before.AssertUnchanged(rig);
            Assert.AreEqual(stageEvents, rig.StageChanges.Count);
            CollectionAssert.IsEmpty(rig.StateChanges);
            Assert.AreEqual(CommandRejection.WrongStage, rig.Observers[0].Results[0].Rejection);
        }

        [Test]
        public void Execute_GateRefuses_TheHandlerNeverRunsNothingIsRaisedAndItIsObserved()
        {
            SessionFlowRig rig = new(gates: 1);
            rig.Begin(SessionFlowFixtures.BranchFirstSystem);
            rig.Gates[0].Permits = false;
            Snapshot before = Snapshot.Of(rig);
            int stageEvents = rig.StageChanges.Count;

            CommandResult result = rig.Flow.Execute(new AbandonSession());

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(CommandRejection.Gated, result.Rejection);
            Assert.AreEqual(SessionStage.BranchConfiguration, result.Stage);
            CollectionAssert.DoesNotContain(rig.Log, "abandoned");
            before.AssertUnchanged(rig);
            Assert.AreEqual(stageEvents, rig.StageChanges.Count);
            Assert.AreEqual(CommandRejection.Gated, rig.Observers[0].Results[0].Rejection);
        }

        [Test]
        public void Execute_Gates_RunInOrderAndStopAtTheFirstRefusal()
        {
            SessionFlowRig rig = new(gates: 3);
            rig.Begin(SessionFlowFixtures.MainSystem);
            rig.Gates[1].Permits = false;
            rig.Log.Clear();

            rig.Flow.Execute(new AbandonSession());

            CollectionAssert.AreEqual(
                new[]
                {
                    "gate G0 AbandonSession Build",
                    "gate G1 AbandonSession Build",
                    "observer O0 AbandonSession Gated",
                },
                rig.Log
            );
        }

        [Test]
        public void Execute_WrongStage_ConsultsNoGate()
        {
            SessionFlowRig rig = new(gates: 1);
            rig.Begin(SessionFlowFixtures.MainSystem);
            rig.Log.Clear();

            rig.Flow.Execute(new SelectRoute(SessionFlowFixtures.RouteOne));

            CollectionAssert.AreEqual(new[] { "observer O0 SelectRoute WrongStage" }, rig.Log);
        }

        [Test]
        public void Execute_Accepted_RaisesEventsThenNotifiesEveryObserverInOrder()
        {
            SessionFlowRig rig = new(gates: 1, observers: 2);
            rig.Begin(SessionFlowFixtures.MainSystem);
            rig.Log.Clear();

            CommandResult result = rig.Flow.Execute(new AbandonSession());

            Assert.IsTrue(result.Accepted);
            CollectionAssert.AreEqual(
                new[]
                {
                    "gate G0 AbandonSession Build",
                    "abandoned",
                    "dispose process scope",
                    "dispose session scope",
                    "stage Build -> SessionEnded",
                    "observer O0 AbandonSession None",
                    "observer O1 AbandonSession None",
                },
                rig.Log
            );
        }

        [Test]
        public void Execute_DomainRefusal_IsDomainRejectedCarriesTheDomainResultRaisesNothingAndIsObserved()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.BranchFirstSystem);
            Snapshot before = Snapshot.Of(rig);
            int stageEvents = rig.StageChanges.Count;

            CommandResult result = rig.Flow.Execute(new ConfigureBranch(BranchAction.Confirm, null));

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual(CommandRejection.DomainRejected, result.Rejection);
            Assert.IsInstanceOf<BranchConfirmationResult>(result.DomainResult);
            Assert.AreEqual(SessionStage.BranchConfiguration, result.Stage);
            before.AssertUnchanged(rig);
            Assert.AreEqual(stageEvents, rig.StageChanges.Count);
            CollectionAssert.IsEmpty(rig.StateChanges);
            Assert.AreEqual(CommandRejection.DomainRejected, rig.Observers[0].Results[0].Rejection);
        }

        [Test]
        public void Execute_FromInsideStageChanged_ThrowsAndTheOuterCommandCompletesAndIsObservedOnce()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.MainSystem);
            Exception nested = null;
            rig.Flow.StageChanged += _ => nested = Assert.Throws<InvalidOperationException>(() => rig.Flow.Execute(new CompileAndExecute()));

            CommandResult result = rig.Flow.Execute(new AbandonSession());

            Assert.IsNotNull(nested, "The nested command was never attempted.");
            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(SessionStage.SessionEnded, result.Stage);
            Assert.AreEqual(1, rig.Observers[0].Results.Count);
        }

        [Test]
        public void BeginSession_FromInsideStageChanged_Throws()
        {
            SessionFlowRig rig = new();
            Exception nested = null;
            rig.Flow.StageChanged += _ => nested = Assert.Throws<InvalidOperationException>(() => rig.Begin(SessionFlowFixtures.MainSystem));

            rig.Begin(SessionFlowFixtures.MainSystem);

            Assert.IsNotNull(nested);
            Assert.AreEqual(1, rig.Scopes.SessionScopesOpened);
        }

        [Test]
        public void Execute_AfterAReentrantRefusal_TheSurfaceAcceptsTheNextCommand()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.MainSystem);
            Action<StageChange> nest = _ => Assert.Throws<InvalidOperationException>(() => rig.Flow.Execute(new CompileAndExecute()));
            rig.Flow.StageChanged += nest;
            rig.Flow.Execute(new AbandonSession());
            rig.Flow.StageChanged -= nest;

            SessionBeginResult again = rig.Begin(SessionFlowFixtures.MainSystem);

            Assert.IsTrue(again.Succeeded);
        }

        /// <summary>
        /// One instance of every command.
        /// </summary>
        /// <returns>The commands.</returns>
        private static List<ISessionCommand> OneOfEach()
        {
            InstanceID item = new(1);
            return new List<ISessionCommand>
            {
                new InstallLine(item, new SourcePosition(3)),
                new RemoveLine(new SourcePosition(3)),
                new MoveLine(new SourcePosition(3), new SourcePosition(4)),
                new SwapLines(new SourcePosition(3), new SourcePosition(4)),
                new ArchiveItem(item),
                new ConfirmDirective(item),
                new CompileAndExecute(),
                new Submit(),
                new Continue(),
                new ReturnToBuild(),
                new PurchaseOffer(1),
                new RerollShop(),
                new PinOffer(1, true),
                new InstallPatch(1, null, item, 1),
                new InstallDependency(1, Array.Empty<InstanceID>()),
                new DestroyDependency(item),
                new DeleteItem(item),
                new DuplicateItem(item),
                new LeaveShop(),
                new ConfigureBranch(BranchAction.Confirm, null),
                new SelectRoute(SessionFlowFixtures.RouteOne),
                new SelectReward(0, SessionFlowFixtures.Instruction, Array.Empty<InstanceID>()),
                new AbandonSession(),
            };
        }

        /// <summary>
        /// The Session's observable holder state at one moment, for before-and-after comparison.
        /// </summary>
        private sealed class Snapshot
        {
            private int _repositoryCount;
            private int _lastAllocated;
            private bool _configuring;
            private bool _branchConfirmed;
            private int _tokens;
            private int _processScopes;
            private int _sessionScopes;

            /// <summary>
            /// Takes a snapshot.
            /// </summary>
            /// <param name="rig">The rig.</param>
            /// <returns>The snapshot.</returns>
            public static Snapshot Of(SessionFlowRig rig)
            {
                SessionState session = rig.Scopes.Session;
                return new Snapshot
                {
                    _repositoryCount = session.Repository.Entries.Count,
                    _lastAllocated = session.InstanceIDs.LastAllocated,
                    _configuring = session.ActiveBranch.HasConfigurationInProgress,
                    _branchConfirmed = session.ActiveBranch.Confirmed != null,
                    _tokens = session.Economy.Tokens.Balance.Value,
                    _processScopes = rig.Scopes.ProcessScopesOpened,
                    _sessionScopes = rig.Scopes.OpenScopes,
                };
            }

            /// <summary>
            /// Asserts the rig's Session still matches the snapshot.
            /// </summary>
            /// <param name="rig">The rig.</param>
            public void AssertUnchanged(SessionFlowRig rig)
            {
                Snapshot now = Of(rig);
                Assert.AreEqual(_repositoryCount, now._repositoryCount, "Repository");
                Assert.AreEqual(_lastAllocated, now._lastAllocated, "Instance identities");
                Assert.AreEqual(_configuring, now._configuring, "Branch configuration");
                Assert.AreEqual(_branchConfirmed, now._branchConfirmed, "Confirmed Branch");
                Assert.AreEqual(_tokens, now._tokens, "Tokens");
                Assert.AreEqual(_processScopes, now._processScopes, "Process scopes");
                Assert.AreEqual(_sessionScopes, now._sessionScopes, "Open scopes");
            }
        }
    }
}
