using System;
using NUnit.Framework;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests <see cref="SessionQueryService"/>. Every view is null outside the stage that owns its
    /// state, and every preview is null outside the stage its operation belongs to. A preview changes
    /// nothing: the Tokens, the Repository and the shop read the same before and after it.
    /// </summary>
    public sealed class SessionQueriesTests
    {
        [Test]
        public void BeforeAnySession_EveryViewAndPreviewIsNull()
        {
            SessionFlowRig rig = new();
            SessionQueryService queries = rig.Queries;

            Assert.AreEqual(SessionStage.NoSession, queries.Stage);
            Assert.IsNull(queries.Session());
            Assert.IsNull(queries.Process());
            Assert.IsNull(queries.Build());
            Assert.IsNull(queries.Branch());
            Assert.IsNull(queries.Shop());
            Assert.IsNull(queries.Result());
            Assert.IsNull(queries.Rewards());
            Assert.IsNull(queries.Routes());
            Assert.IsNull(queries.PreviewPurchase(1));
            Assert.IsNull(queries.PreviewReroll());
            Assert.IsNull(queries.PreviewPatch(1, null, new InstanceID(1), 1));
            Assert.IsNull(queries.PreviewDependency(1, Array.Empty<InstanceID>()));
            Assert.IsNull(queries.PreviewDestroy(new InstanceID(1)));
            Assert.IsNull(queries.PreviewDelete(new InstanceID(1)));
            Assert.IsNull(queries.PreviewDuplicate(new InstanceID(1)));
        }

        [Test]
        public void InBuild_OnlyTheSessionProcessAndBuildViewsExist()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PlaySystem);

            Assert.IsNotNull(rig.Queries.Session());
            Assert.IsNotNull(rig.Queries.Process());
            Assert.IsNotNull(rig.Queries.Build());
            Assert.IsNull(rig.Queries.Branch());
            Assert.IsNull(rig.Queries.Shop());
            Assert.IsNull(rig.Queries.Result());
            Assert.IsNull(rig.Queries.Rewards());
            Assert.IsNull(rig.Queries.Routes());
            Assert.IsNull(rig.Queries.PreviewPurchase(1));
        }

        [Test]
        public void InResultReview_TheBuildViewIsNullAndTheResultViewExists()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            rig.Flow.Execute(new CompileAndExecute());

            Assert.IsNull(rig.Queries.Build());
            Assert.IsNotNull(rig.Queries.Result());
            Assert.IsNotNull(rig.Queries.Process());
        }

        [Test]
        public void ShopPreviews_ReportPricesAndChangeNothing()
        {
            SessionFlowRig rig = PlayIntoTheShop();
            SessionState session = rig.Scopes.Session;
            int tokens = session.Economy.Tokens.Balance.Value;
            int entries = session.Repository.Entries.Count;
            InstanceID host = session.Repository.EntriesOf(SessionFlowFixtures.Instruction)[0].Item.InstanceID;

            PurchasePreview purchase = rig.Queries.PreviewPurchase(1);
            RerollPreview reroll = rig.Queries.PreviewReroll();
            PatchAttachmentPreview patch = rig.Queries.PreviewPatch(3, null, host, 1);
            DependencyInstallationPreview dependency = rig.Queries.PreviewDependency(4, Array.Empty<InstanceID>());
            ServicePreview duplicate = rig.Queries.PreviewDuplicate(host);

            Assert.AreEqual(ShopRejection.None, purchase.Rejection);
            Assert.AreEqual(1, purchase.Price);
            Assert.AreEqual(ShopRejection.None, reroll.Rejection);
            Assert.AreEqual(PatchAttachmentRejection.None, patch.Rejection);
            Assert.AreEqual(DependencyRejection.None, dependency.Rejection);
            Assert.AreEqual(ServiceRejection.None, duplicate.Rejection);
            Assert.AreEqual(tokens, session.Economy.Tokens.Balance.Value);
            Assert.AreEqual(entries, session.Repository.Entries.Count);
            Assert.AreEqual(0, rig.Queries.Shop().RerollsUsed);
        }

        [Test]
        public void PreviewDestroy_TheStarter_IsRefusedAsPermanent()
        {
            SessionFlowRig rig = PlayIntoTheShop();
            InstanceID starter = rig.Scopes.Session.Economy.Dependencies.AllInstalled[0].InstanceID;

            DependencyDestructionPreview preview = rig.Queries.PreviewDestroy(starter);

            Assert.AreEqual(DependencyRejection.StarterPermanent, preview.Rejection);
        }

        [Test]
        public void ServicePreviews_InAShopWithoutServices_AreRefusedAsNotOffered()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.ShopFirstSystem);
            InstanceID item = rig.Scopes.Session.Repository.Entries[0].Item.InstanceID;

            Assert.AreEqual(ServiceRejection.ServicesNotOffered, rig.Queries.PreviewDelete(item).Rejection);
            Assert.AreEqual(ServiceRejection.ServicesNotOffered, rig.Queries.PreviewDuplicate(item).Rejection);
        }

        [Test]
        public void PreviewPatch_ForAGrantAtRewards_DoesNotConsumeTheGrant()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PatchSystem);
            RewardCommandTests.PassAndSubmit(rig, SessionFlowFixtures.Instruction);
            SessionState session = rig.Scopes.Session;
            InstanceID grant = session.Economy.PatchGrants[0].GrantIdentity;
            InstanceID host = session.Repository.EntriesOf(SessionFlowFixtures.Instruction)[1].Item.InstanceID;

            PatchAttachmentPreview preview = rig.Queries.PreviewPatch(null, grant, host, 1);

            Assert.AreEqual(PatchAttachmentRejection.None, preview.Rejection);
            Assert.AreEqual(0, preview.Price);
            Assert.IsTrue(session.Economy.TryGetGrant(grant, out PatchGrant _));
            Assert.AreEqual(SessionStage.Rewards, rig.Queries.Stage);
        }

        [Test]
        public void AfterAbandonment_EveryViewIsNullAndTheEndReasonIsAbandoned()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            rig.Flow.Execute(new AbandonSession());

            Assert.AreEqual(SessionStage.SessionEnded, rig.Queries.Stage);
            Assert.AreEqual(SessionEndReason.Abandoned, rig.Queries.EndReason);
            Assert.IsNull(rig.Queries.Session());
            Assert.IsNull(rig.Queries.Process());
            Assert.IsNull(rig.Queries.Build());
        }

        /// <summary>
        /// Plays the first play Process at Pass and stands in the shop after it.
        /// </summary>
        /// <returns>The rig in the shop.</returns>
        private static SessionFlowRig PlayIntoTheShop()
        {
            SessionFlowRig rig = new();
            rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            RewardCommandTests.PassAndSubmit(rig, SessionFlowPlayContent.OverwriteInstruction);
            Assert.AreEqual(SessionStage.Shop, rig.Queries.Stage);
            return rig;
        }
    }
}
