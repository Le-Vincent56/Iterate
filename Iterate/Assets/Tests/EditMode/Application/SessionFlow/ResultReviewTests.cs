using NUnit.Framework;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests the Result Review exits. Below Pass with executions remaining, only Return to Build
    /// leaves. At Pass or above, Submit takes the rewards and Continue discards the result to build
    /// again. The final execution needs no exit: at Pass it submits itself. Returning to Build fires
    /// the arrival moment that follows the execution just run.
    ///
    /// The play Processes allow two executions. An empty source outputs 1 (below Pass), the Overwrite
    /// Instruction alone outputs 2 (Pass), and a scoring Instruction outputs 3 (Optimize).
    /// </summary>
    public sealed class ResultReviewTests
    {
        private SessionFlowRig _rig;
        private SessionState _session;

        [SetUp]
        public void SetUp()
        {
            _rig = new SessionFlowRig();
            _rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            _session = _rig.Scopes.Session;
        }

        [Test]
        public void BelowPass_ReturnToBuild_IsAcceptedAndReturnsToBuild()
        {
            _rig.Flow.Execute(new CompileAndExecute());

            CommandResult result = _rig.Flow.Execute(new ReturnToBuild());

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(SessionStage.Build, result.Stage);
            Assert.IsInstanceOf<ArrivalResult>(result.DomainResult);
        }

        [Test]
        public void BelowPass_SubmitAndContinue_AreRefused()
        {
            _rig.Flow.Execute(new CompileAndExecute());

            CommandResult submit = _rig.Flow.Execute(new Submit());
            CommandResult discard = _rig.Flow.Execute(new Continue());

            Assert.AreEqual(CommandRejection.DomainRejected, submit.Rejection);
            Assert.AreEqual(SubmitRejection.BelowPass, ((SubmitResult)submit.DomainResult).Rejection);
            Assert.AreEqual(CommandRejection.DomainRejected, discard.Rejection);
            Assert.IsNull(((ThresholdEvaluation)discard.DomainResult).Reached);
            Assert.AreEqual(SessionStage.ResultReview, discard.Stage);
        }

        [Test]
        public void AtPass_ReturnToBuild_IsRefused()
        {
            InstallAndExecute(SessionFlowPlayContent.OverwriteInstruction);

            CommandResult result = _rig.Flow.Execute(new ReturnToBuild());

            Assert.AreEqual(CommandRejection.DomainRejected, result.Rejection);
            Assert.AreEqual(RewardTier.Pass, ((ThresholdEvaluation)result.DomainResult).Reached);
            Assert.AreEqual(SessionStage.ResultReview, result.Stage);
        }

        [Test]
        public void AtPass_Continue_ReturnsToBuildAndTheDiscardedResultCannotBeSubmittedFromBuild()
        {
            InstallAndExecute(SessionFlowPlayContent.OverwriteInstruction);

            CommandResult discard = _rig.Flow.Execute(new Continue());
            CommandResult submit = _rig.Flow.Execute(new Submit());

            Assert.IsTrue(discard.Accepted);
            Assert.AreEqual(SessionStage.Build, discard.Stage);
            Assert.AreEqual(CommandRejection.WrongStage, submit.Rejection, "Submit is legal only in Result Review, which only a new execution enters.");
        }

        [Test]
        public void AtPass_Submit_TakesTheRewardsAndMovesOnWhenNothingIsPending()
        {
            InstallAndExecute(SessionFlowPlayContent.OverwriteInstruction);
            int tokensBefore = _session.Economy.Tokens.Balance.Value;

            CommandResult result = _rig.Flow.Execute(new Submit());

            Assert.IsTrue(result.Accepted);
            SubmitResult submitted = (SubmitResult)result.DomainResult;
            Assert.IsTrue(submitted.Succeeded);
            Assert.IsTrue(submitted.Rewards.IsComplete);
            Assert.AreEqual(tokensBefore + 10, _session.Economy.Tokens.Balance.Value);
            Assert.AreEqual(SessionStage.Shop, result.Stage, "Rewards with nothing pending complete inside Submit.");
            CollectionAssert.Contains(_rig.Log, "dispose process scope");
        }

        [Test]
        public void FinalExecution_AtPass_SubmitsItself()
        {
            _rig.Flow.Execute(new CompileAndExecute());
            _rig.Flow.Execute(new ReturnToBuild());

            CommandResult result = InstallAndExecute(SessionFlowPlayContent.OverwriteInstruction);

            Assert.IsTrue(result.Accepted);
            Assert.IsInstanceOf<ExecutionOutcome>(result.DomainResult);
            Assert.AreEqual(SessionStage.Shop, result.Stage);
            Assert.AreEqual(10, _session.Economy.Tokens.Balance.Value);
        }

        [Test]
        public void MandatoryExecutions_SubmitWhileSomeRemain_IsRefused()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.MandatorySystem);
            rig.Flow.Execute(new InstallLine(rig.Scopes.Session.Repository.EntriesOf(SessionFlowPlayContent.OverwriteInstruction)[0].Item.InstanceID, new SourcePosition(2)));
            rig.Flow.Execute(new CompileAndExecute());

            CommandResult result = rig.Flow.Execute(new Submit());

            Assert.AreEqual(SubmitRejection.MandatoryExecutionsRemain, ((SubmitResult)result.DomainResult).Rejection);
            Assert.AreEqual(SessionStage.ResultReview, result.Stage);
        }

        [Test]
        public void ReturnToBuild_FiresTheArrivalAfterTheExecutionJustRun()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.ArrivalSystem);
            ProcessState process = rig.Scopes.Process;
            rig.Flow.Execute(new CompileAndExecute());

            CommandResult result = rig.Flow.Execute(new ReturnToBuild());

            ArrivalResult arrived = (ArrivalResult)result.DomainResult;
            Assert.IsTrue(arrived.Succeeded);
            Assert.AreEqual(1, arrived.Arrived.Count);
            Assert.AreEqual(SessionFlowFixtures.Instruction, arrived.Arrived[0].DefinitionID);
            Assert.AreEqual(1, process.Counters.ExecutionsRun);
        }

        /// <summary>
        /// Installs the first entry of a definition at line 2 and executes.
        /// </summary>
        /// <param name="definition">The content ID.</param>
        /// <returns>The execution's command result.</returns>
        private CommandResult InstallAndExecute(string definition)
        {
            InstanceID item = _session.Repository.EntriesOf(definition)[0].Item.InstanceID;
            _rig.Flow.Execute(new InstallLine(item, new SourcePosition(2)));
            return _rig.Flow.Execute(new CompileAndExecute());
        }
    }
}
