using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests <see cref="ConfigureBranch"/> in Branch configuration over the first route Process, whose
    /// Branch holds two items with the Structure required. The three-item starter Repository seeds an
    /// over-capacity draft, so confirming is refused until an Instruction is excluded. A refused edit
    /// or confirmation leaves the draft as it was and the stage where it was.
    /// </summary>
    public sealed class BranchCommandTests
    {
        private SessionFlowRig _rig;
        private SessionState _session;

        [SetUp]
        public void SetUp()
        {
            _rig = new SessionFlowRig();
            _rig.Begin(SessionFlowFixtures.BranchFirstSystem);
            _session = _rig.Scopes.Session;
        }

        [Test]
        public void Exclude_AnInstruction_IsAcceptedAndReportsTheBranch()
        {
            InstanceID instruction = EntryOf(SessionFlowFixtures.Instruction, 0);

            CommandResult result = _rig.Flow.Execute(new ConfigureBranch(BranchAction.Exclude, instruction));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(SessionStage.BranchConfiguration, result.Stage);
            Assert.IsInstanceOf<BranchEditResult>(result.DomainResult);
            Assert.IsFalse(_session.ActiveBranch.StateOf(instruction).HasFlag(BranchInstanceState.Selected));
            Assert.AreEqual(new StateChange(ChangedAreas.Branch), _rig.StateChanges[0]);
        }

        [Test]
        public void Include_AnExcludedInstruction_IsAcceptedAndSelectsItAgain()
        {
            InstanceID instruction = EntryOf(SessionFlowFixtures.Instruction, 0);
            _rig.Flow.Execute(new ConfigureBranch(BranchAction.Exclude, instruction));

            CommandResult result = _rig.Flow.Execute(new ConfigureBranch(BranchAction.Include, instruction));

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(_session.ActiveBranch.StateOf(instruction).HasFlag(BranchInstanceState.Selected));
        }

        [Test]
        public void Exclude_TheRequiredStructure_IsRefusedAndLeavesTheDraftUnchanged()
        {
            InstanceID structure = EntryOf(SessionFlowFixtures.Structure, 0);
            BranchInstanceState before = _session.ActiveBranch.StateOf(structure);
            BranchValidation validationBefore = _session.ActiveBranch.Validate();

            CommandResult result = _rig.Flow.Execute(new ConfigureBranch(BranchAction.Exclude, structure));

            Assert.AreEqual(CommandRejection.DomainRejected, result.Rejection);
            Assert.AreEqual(BranchEditRejection.RequiredCannotBeExcluded, ((BranchEditResult)result.DomainResult).Rejection);
            Assert.AreEqual(before, _session.ActiveBranch.StateOf(structure));
            Assert.AreEqual(validationBefore.IsOverCapacity, _session.ActiveBranch.Validate().IsOverCapacity);
            CollectionAssert.IsEmpty(_rig.StateChanges);
        }

        [Test]
        public void Confirm_OverCapacity_IsRefusedAndStaysInBranchConfiguration()
        {
            CommandResult result = _rig.Flow.Execute(new ConfigureBranch(BranchAction.Confirm, null));

            Assert.AreEqual(CommandRejection.DomainRejected, result.Rejection);
            BranchConfirmationResult confirmation = (BranchConfirmationResult)result.DomainResult;
            Assert.IsTrue(confirmation.Validation.IsOverCapacity);
            Assert.AreEqual(SessionStage.BranchConfiguration, result.Stage);
            Assert.IsTrue(_session.ActiveBranch.HasConfigurationInProgress);
            Assert.IsNull(_session.ActiveBranch.Confirmed);
            Assert.AreEqual(0, _rig.Scopes.ProcessScopesOpened);
        }

        [Test]
        public void Confirm_WithinCapacity_EntersTheProcessInBuild()
        {
            _rig.Flow.Execute(new ConfigureBranch(BranchAction.Exclude, EntryOf(SessionFlowFixtures.Instruction, 0)));

            CommandResult result = _rig.Flow.Execute(new ConfigureBranch(BranchAction.Confirm, null));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(SessionStage.Build, result.Stage);
            BranchConfirmationResult confirmation = (BranchConfirmationResult)result.DomainResult;
            Assert.AreEqual(2, confirmation.Record.Eligible.Count);
            Assert.IsFalse(_session.ActiveBranch.HasConfigurationInProgress);
            Assert.AreEqual(SessionFlowFixtures.RouteOneProcess.Value, _rig.Scopes.Process.ProcessIdentity);
            Assert.AreEqual(new StageChange(SessionStage.BranchConfiguration, SessionStage.Build, SessionEndReason.None), _rig.StageChanges[_rig.StageChanges.Count - 1]);
            Assert.AreEqual(
                ChangedAreas.Branch | ChangedAreas.Build | ChangedAreas.Buffer | ChangedAreas.Bytes | ChangedAreas.Counters,
                _rig.StateChanges[_rig.StateChanges.Count - 1].Areas
            );
        }

        [Test]
        public void IncludeOrExclude_WithNoItem_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _rig.Flow.Execute(new ConfigureBranch(BranchAction.Exclude, null)));
            Assert.Throws<ArgumentException>(() => _rig.Flow.Execute(new ConfigureBranch(BranchAction.Include, null)));
        }

        [Test]
        public void Confirm_NamingAnItem_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _rig.Flow.Execute(new ConfigureBranch(BranchAction.Confirm, new InstanceID(1))));
        }

        [Test]
        public void Confirm_WhenTheProcessCannotBeCreated_IsDomainRejectedCarryingTheCreationAndOpensNothing()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.MissingCoreSystem);
            SessionState session = rig.Scopes.Session;
            rig.Flow.Execute(new ConfigureBranch(BranchAction.Exclude, session.Repository.EntriesOf(SessionFlowFixtures.Instruction)[0].Item.InstanceID));

            CommandResult result = rig.Flow.Execute(new ConfigureBranch(BranchAction.Confirm, null));

            Assert.AreEqual(CommandRejection.DomainRejected, result.Rejection);
            ProcessCreationResult creation = (ProcessCreationResult)result.DomainResult;
            Assert.AreEqual(ProcessCreationRejection.CoreMissing, creation.Rejection);
            Assert.AreEqual(SessionStage.BranchConfiguration, result.Stage);
            Assert.AreEqual(0, rig.Scopes.ProcessScopesOpened);
            Assert.AreEqual(SessionStage.BranchConfiguration, rig.Stage);
        }

        /// <summary>
        /// The instance identity of one Repository entry of a definition.
        /// </summary>
        /// <param name="definition">The content ID.</param>
        /// <param name="index">Which entry of that definition.</param>
        /// <returns>The instance identity.</returns>
        private InstanceID EntryOf(string definition, int index)
        {
            IReadOnlyList<RepositoryEntry> entries = _session.Repository.EntriesOf(definition);
            return entries[index].Item.InstanceID;
        }
    }
}
