using NUnit.Framework;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Compilation;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests <see cref="CompileAndExecute"/>: a compilation the Bytes cannot pay is refused before
    /// anything is spent. A committed one pays exactly its final cost, records one execution, holds
    /// the result and enters Result Review. The execution's identity is the Process's own and counts
    /// executions within the Process.
    /// </summary>
    public sealed class CompileAndExecuteTests
    {
        [Test]
        public void Execute_WhenTheBytesCannotPay_IsRefusedWithTheAttemptAndSpendsNothing()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.BrokeSystem);
            ProcessState process = rig.Scopes.Process;
            rig.Flow.Execute(new CompileAndExecute());
            rig.Flow.Execute(new ReturnToBuild());
            rig.Flow.Execute(new InstallLine(Item(rig, SessionFlowFixtures.Instruction), new SourcePosition(2)));
            int bytesBefore = process.Bytes.Balance.Value;

            CommandResult result = rig.Flow.Execute(new CompileAndExecute());

            Assert.AreEqual(CommandRejection.DomainRejected, result.Rejection);
            CompilationAttempt attempt = (CompilationAttempt)result.DomainResult;
            Assert.IsFalse(attempt.Committed);
            Assert.AreEqual(CompilationBlockReason.InsufficientBytes, attempt.BlockReason);
            Assert.AreEqual(bytesBefore, process.Bytes.Balance.Value);
            Assert.AreEqual(1, process.Counters.ExecutionsRun, "Only the free initial compilation ran.");
            Assert.AreEqual(SessionStage.Build, result.Stage);
        }

        [Test]
        public void Execute_Committed_PaysTheFinalCostRecordsOneExecutionAndEntersResultReview()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            ProcessState process = rig.Scopes.Process;
            rig.Flow.Execute(new InstallLine(Item(rig, SessionFlowFixtures.Instruction), new SourcePosition(2)));
            int bytesBefore = process.Bytes.Balance.Value;
            rig.StateChanges.Clear();

            CommandResult result = rig.Flow.Execute(new CompileAndExecute());

            Assert.IsTrue(result.Accepted);
            ExecutionOutcome outcome = (ExecutionOutcome)result.DomainResult;
            Assert.IsTrue(outcome.Attempt.Committed);
            Assert.AreEqual(bytesBefore - outcome.Attempt.Breakdown.FinalCost, process.Bytes.Balance.Value);
            Assert.AreEqual(1, process.Counters.ExecutionsRun);
            Assert.AreEqual(SessionStage.ResultReview, result.Stage);
            Assert.AreEqual(new StateChange(ChangedAreas.Bytes | ChangedAreas.Counters | ChangedAreas.Execution), rig.StateChanges[0]);
        }

        [Test]
        public void Execute_AnEditedSourceAfterTheInitialCompilation_PaysItsOrdinaryCost()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            ProcessState process = rig.Scopes.Process;
            ExecutionOutcome initial = (ExecutionOutcome)rig.Flow.Execute(new CompileAndExecute()).DomainResult;
            rig.Flow.Execute(new ReturnToBuild());
            rig.Flow.Execute(new InstallLine(Item(rig, SessionFlowFixtures.Instruction), new SourcePosition(2)));
            int bytesBefore = process.Bytes.Balance.Value;

            ExecutionOutcome edited = (ExecutionOutcome)rig.Flow.Execute(new CompileAndExecute()).DomainResult;

            Assert.AreEqual(0, initial.Attempt.Breakdown.FinalCost, "A Process's first compilation is free.");
            Assert.Greater(edited.Attempt.Breakdown.FinalCost, 0);
            Assert.AreEqual(bytesBefore - edited.Attempt.Breakdown.FinalCost, process.Bytes.Balance.Value);
        }

        [Test]
        public void Execute_OneScoringInstruction_ReachesOptimize()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            rig.Flow.Execute(new InstallLine(Item(rig, SessionFlowFixtures.Instruction), new SourcePosition(2)));

            ExecutionOutcome outcome = (ExecutionOutcome)rig.Flow.Execute(new CompileAndExecute()).DomainResult;

            Assert.AreEqual(3, outcome.Record.FinalState.FinalOutput.Value);
            Assert.AreEqual(RewardTier.Optimize, outcome.Evaluation.Reached);
        }

        [Test]
        public void Execute_AnEmptySource_IsBelowPass()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PlaySystem);

            ExecutionOutcome outcome = (ExecutionOutcome)rig.Flow.Execute(new CompileAndExecute()).DomainResult;

            Assert.AreEqual(1, outcome.Record.FinalState.FinalOutput.Value);
            Assert.IsNull(outcome.Evaluation.Reached);
        }

        [Test]
        public void Execute_IdentitiesNameTheProcessAndCountItsExecutions()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PlaySystem);

            ExecutionOutcome first = (ExecutionOutcome)rig.Flow.Execute(new CompileAndExecute()).DomainResult;
            rig.Flow.Execute(new ReturnToBuild());
            rig.Flow.Execute(new InstallLine(Item(rig, SessionFlowFixtures.Instruction), new SourcePosition(2)));
            ExecutionOutcome second = (ExecutionOutcome)rig.Flow.Execute(new CompileAndExecute()).DomainResult;

            Assert.AreEqual("WB-PROC-101:execution:1", first.Record.Header.ExecutionIdentity);
            Assert.AreEqual("WB-PROC-101:execution:2", second.Record.Header.ExecutionIdentity);
        }

        /// <summary>
        /// The first Repository entry of a definition in the rig's Session.
        /// </summary>
        /// <param name="rig">The rig.</param>
        /// <param name="definition">The content ID.</param>
        /// <returns>The instance identity.</returns>
        private static InstanceID Item(SessionFlowRig rig, string definition)
        {
            return rig.Scopes.Session.Repository.EntriesOf(definition)[0].Item.InstanceID;
        }
    }
}
