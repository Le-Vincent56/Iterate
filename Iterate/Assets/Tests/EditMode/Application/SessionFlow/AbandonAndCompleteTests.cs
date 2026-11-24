using NUnit.Framework;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests how a Session ends. Abandonment is legal in every live stage: the lifecycle hears of it
    /// once, the Process scope closes before the Session scope, and the Session ends as abandoned.
    /// Disposal is the reset, so nothing stays open. Each live stage is reached through a fixture
    /// System that begins on it; Result Review and Rewards are reached by playing, and their abandon cases live with their own suites.
    /// </summary>
    public sealed class AbandonAndCompleteTests
    {
        [Test]
        public void Abandon_FromBuild_ClosesTheProcessScopeBeforeTheSessionScope()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.MainSystem);
            rig.Log.Clear();

            CommandResult result = rig.Flow.Execute(new AbandonSession());

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(SessionStage.SessionEnded, result.Stage);
            CollectionAssert.AreEqual(
                new[] { "abandoned", "dispose process scope", "dispose session scope" },
                rig.Log.GetRange(0, 3)
            );
            AssertEndedAbandoned(rig);
        }

        [TestCase("WB-SYS-SHOP", SessionStage.Shop)]
        [TestCase("WB-SYS-ROUTE", SessionStage.RouteSelection)]
        [TestCase("WB-SYS-BRANCH", SessionStage.BranchConfiguration)]
        public void Abandon_FromAStageWithNoProcess_TellsTheLifecycleOnceAndClosesTheSessionScope(string system, SessionStage stage)
        {
            SessionFlowRig rig = new();
            rig.Begin(new SystemID(system));
            Assert.AreEqual(stage, rig.Stage);
            rig.Log.Clear();

            CommandResult result = rig.Flow.Execute(new AbandonSession());

            Assert.IsTrue(result.Accepted);
            CollectionAssert.AreEqual(new[] { "abandoned", "dispose session scope" }, rig.Log.GetRange(0, 2));
            AssertEndedAbandoned(rig);
        }

        [Test]
        public void Abandon_RaisesOneStageChangeToSessionEndedAndNoStateChange()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.MainSystem);
            rig.StageChanges.Clear();

            rig.Flow.Execute(new AbandonSession());

            Assert.AreEqual(1, rig.StageChanges.Count);
            Assert.AreEqual(new StageChange(SessionStage.Build, SessionStage.SessionEnded, SessionEndReason.Abandoned), rig.StageChanges[0]);
            CollectionAssert.IsEmpty(rig.StateChanges);
        }

        [Test]
        public void Abandon_Twice_TheSecondIsNoSessionAndTellsTheLifecycleNothing()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.MainSystem);
            rig.Flow.Execute(new AbandonSession());
            rig.Log.Clear();

            CommandResult second = rig.Flow.Execute(new AbandonSession());

            Assert.AreEqual(CommandRejection.NoSession, second.Rejection);
            CollectionAssert.DoesNotContain(rig.Log, "abandoned");
        }

        [Test]
        public void Complete_AfterTheLastProcessRewards_TellsTheLifecycleOnceAndEndsCompleted()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PatchSystem);

            CommandResult result = RewardCommandTests.PassAndSubmitResult(rig, SessionFlowPlayContent.OverwriteInstruction);

            Assert.AreEqual(SessionStage.SessionEnded, result.Stage);
            Assert.AreEqual(new StageChange(SessionStage.ResultReview, SessionStage.SessionEnded, SessionEndReason.Completed), rig.StageChanges[rig.StageChanges.Count - 1]);
            Assert.AreEqual(1, CountOf(rig, "completed"));
            Assert.AreEqual(0, CountOf(rig, "abandoned"));
            Assert.AreEqual(0, rig.Scopes.OpenScopes);
        }

        /// <summary>
        /// Asserts the Session ended as abandoned with every scope closed and the lifecycle told once.
        /// </summary>
        /// <param name="rig">The rig.</param>
        private static void AssertEndedAbandoned(SessionFlowRig rig)
        {
            Assert.AreEqual(SessionStage.SessionEnded, rig.Stage);
            StageChange last = rig.StageChanges[rig.StageChanges.Count - 1];
            Assert.AreEqual(SessionEndReason.Abandoned, last.EndReason);
            Assert.AreEqual(0, rig.Scopes.OpenScopes);
            Assert.AreEqual(1, CountOf(rig, "abandoned"));
            Assert.AreEqual(0, CountOf(rig, "completed"));
        }

        /// <summary>
        /// Counts a log line.
        /// </summary>
        /// <param name="rig">The rig.</param>
        /// <param name="line">The line.</param>
        /// <returns>How often it was logged.</returns>
        private static int CountOf(SessionFlowRig rig, string line)
        {
            int count = 0;
            foreach (string logged in rig.Log)
            {
                if (logged == line)
                    count++;
            }

            return count;
        }
    }
}
