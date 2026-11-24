using System;
using NUnit.Framework;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Compilation;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests the rewards stage. It blocks until every choice is made and every granted Patch is
    /// attached, and nothing but those two commands and abandonment is legal inside it. The last
    /// resolution expires the Process's Utilities, closes its scope and moves on, which after the last
    /// Process completes the Session.
    /// </summary>
    public sealed class RewardCommandTests
    {
        [Test]
        public void PendingChoice_BlocksUntilSelectRewardAndOtherCommandsAreWrongStage()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.ChoiceSystem);
            SubmitResult submitted = PassAndSubmit(rig, SessionFlowPlayContent.OverwriteInstruction);
            Assert.AreEqual(SessionStage.Rewards, rig.Stage);
            PendingReward pending = submitted.Rewards.Pending[0];

            CommandResult wrong = rig.Flow.Execute(new CompileAndExecute());
            CommandResult chosen = rig.Flow.Execute(new SelectReward(pending.ComponentIndex, SessionFlowFixtures.OfferedInstruction, Array.Empty<InstanceID>()));

            Assert.AreEqual(CommandRejection.WrongStage, wrong.Rejection);
            Assert.IsTrue(chosen.Accepted);
            Assert.AreEqual(1, rig.Scopes.Session.Repository.EntriesOf(SessionFlowFixtures.OfferedInstruction).Count);
            Assert.AreEqual(SessionStage.SessionEnded, chosen.Stage);
        }

        [Test]
        public void SelectReward_AContentOutsideThePool_IsRefusedAndStaysInRewards()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.ChoiceSystem);
            SubmitResult submitted = PassAndSubmit(rig, SessionFlowPlayContent.OverwriteInstruction);

            CommandResult result = rig.Flow.Execute(new SelectReward(submitted.Rewards.Pending[0].ComponentIndex, SessionFlowPlayContent.Patch, Array.Empty<InstanceID>()));

            Assert.AreEqual(CommandRejection.DomainRejected, result.Rejection);
            Assert.AreEqual(RewardRejection.NotAPoolMember, ((RewardChoiceResult)result.DomainResult).Rejection);
            Assert.AreEqual(SessionStage.Rewards, result.Stage);
        }

        [Test]
        public void PatchGrant_AtOptimize_BlocksUntilInstallPatchAttachesIt()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PatchSystem);
            PassAndSubmit(rig, SessionFlowFixtures.Instruction);
            SessionState session = rig.Scopes.Session;
            Assert.AreEqual(SessionStage.Rewards, rig.Stage);
            InstanceID grant = session.Economy.PatchGrants[0].GrantIdentity;
            InstanceID host = session.Repository.EntriesOf(SessionFlowFixtures.Instruction)[1].Item.InstanceID;

            CommandResult result = rig.Flow.Execute(new InstallPatch(null, grant, host, 1));

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(((PatchAttachmentResult)result.DomainResult).Succeeded);
            Assert.IsFalse(session.Economy.TryGetGrant(grant, out PatchGrant _), "The grant is consumed.");
            Assert.AreEqual(SessionStage.SessionEnded, result.Stage);
        }

        [Test]
        public void PatchGrant_AnUnknownGrant_IsRefusedAndStaysInRewards()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PatchSystem);
            PassAndSubmit(rig, SessionFlowFixtures.Instruction);
            InstanceID host = rig.Scopes.Session.Repository.EntriesOf(SessionFlowFixtures.Instruction)[1].Item.InstanceID;

            CommandResult result = rig.Flow.Execute(new InstallPatch(null, new InstanceID(999), host, 1));

            Assert.AreEqual(PatchAttachmentRejection.UnknownGrant, ((PatchAttachmentResult)result.DomainResult).Rejection);
            Assert.AreEqual(SessionStage.Rewards, result.Stage);
        }

        [Test]
        public void InstallPatch_InRewardsWithASlot_ThrowsArgumentException()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PatchSystem);
            PassAndSubmit(rig, SessionFlowFixtures.Instruction);

            Assert.Throws<ArgumentException>(() => rig.Flow.Execute(new InstallPatch(1, null, new InstanceID(1), 1)));
        }

        [Test]
        public void Utilities_CommittedToAProcess_ExpireWhenItsRewardsResolve()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            PassAndSubmit(rig, SessionFlowPlayContent.OverwriteInstruction);
            SessionState session = rig.Scopes.Session;
            rig.Flow.Execute(new PurchaseOffer(2));
            Assert.AreEqual(1, session.Economy.Utilities.Count);
            rig.Flow.Execute(new LeaveShop());

            CommandResult result = PassAndSubmitResult(rig, SessionFlowPlayContent.OverwriteInstruction);

            Assert.AreEqual(SessionStage.SessionEnded, result.Stage);
            Assert.AreEqual(0, session.Economy.Utilities.Count);
        }

        [Test]
        public void Abandon_FromRewards_EndsTheSessionAsAbandoned()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.ChoiceSystem);
            PassAndSubmit(rig, SessionFlowPlayContent.OverwriteInstruction);
            rig.Log.Clear();

            CommandResult result = rig.Flow.Execute(new AbandonSession());

            Assert.AreEqual(SessionStage.SessionEnded, result.Stage);
            CollectionAssert.AreEqual(new[] { "abandoned", "dispose process scope", "dispose session scope" }, rig.Log.GetRange(0, 3));
        }

        /// <summary>
        /// Installs the first entry of a definition, executes, and submits.
        /// </summary>
        /// <param name="rig">The rig.</param>
        /// <param name="definition">The content ID to install.</param>
        /// <returns>The submit result.</returns>
        internal static SubmitResult PassAndSubmit(SessionFlowRig rig, string definition)
        {
            return (SubmitResult)PassAndSubmitResult(rig, definition).DomainResult;
        }

        /// <summary>
        /// Installs the first entry of a definition, executes, and submits.
        /// </summary>
        /// <param name="rig">The rig.</param>
        /// <param name="definition">The content ID to install.</param>
        /// <returns>The submit command's result.</returns>
        internal static CommandResult PassAndSubmitResult(SessionFlowRig rig, string definition)
        {
            InstanceID item = rig.Scopes.Session.Repository.EntriesOf(definition)[0].Item.InstanceID;
            Assert.IsTrue(rig.Flow.Execute(new InstallLine(item, new SourcePosition(2))).Accepted);
            Assert.AreEqual(SessionStage.ResultReview, rig.Flow.Execute(new CompileAndExecute()).Stage);
            CommandResult submitted = rig.Flow.Execute(new Submit());
            Assert.IsTrue(submitted.Accepted);
            return submitted;
        }
    }
}
