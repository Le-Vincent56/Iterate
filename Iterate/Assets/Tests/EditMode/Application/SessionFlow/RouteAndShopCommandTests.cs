using System;
using NUnit.Framework;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Compilation;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests route choice and the shop. The full play shop is reached by passing the first play
    /// Process, whose rewards credit ten Tokens: slot 1 is an Instruction, slot 2 a Utility, slot 3 a
    /// Patch and slot 4 a Dependency, each priced at one Token. Patch and Dependency purchases install
    /// as they are bought, so leaving is accepted straight after either one. A refused operation
    /// leaves the Tokens and the Repository as they were.
    /// </summary>
    public sealed class RouteAndShopCommandTests
    {
        private SessionFlowRig _rig;
        private SessionState _session;

        [SetUp]
        public void SetUp()
        {
            _rig = new SessionFlowRig();
            _rig.BeginPlay(SessionFlowPlayContent.PlaySystem);
            _session = _rig.Scopes.Session;
            _rig.Flow.Execute(new InstallLine(Item(SessionFlowPlayContent.OverwriteInstruction), new SourcePosition(2)));
            _rig.Flow.Execute(new CompileAndExecute());
            Assert.AreEqual(SessionStage.Shop, _rig.Flow.Execute(new Submit()).Stage);
        }

        [Test]
        public void SelectRoute_AnOfferedRoute_CommitsItAndOpensTheRouteShop()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.RouteFirstSystem);

            CommandResult result = rig.Flow.Execute(new SelectRoute(SessionFlowFixtures.RouteTwo));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(SessionStage.Shop, result.Stage);
            rig.Flow.Execute(new LeaveShop());
            Assert.AreEqual(SessionStage.BranchConfiguration, rig.Stage);
            rig.Flow.Execute(new ConfigureBranch(BranchAction.Exclude, rig.Scopes.Session.Repository.EntriesOf(SessionFlowFixtures.Instruction)[0].Item.InstanceID));
            rig.Flow.Execute(new ConfigureBranch(BranchAction.Confirm, null));
            Assert.AreEqual(SessionFlowFixtures.RouteTwoProcess.Value, rig.Scopes.Process.ProcessIdentity);
            Assert.AreEqual(SessionFlowFixtures.RouteTwo, rig.Lifecycle.RoutesAtEntry[0]);
        }

        [Test]
        public void SelectRoute_ARouteNotOffered_IsRefusedAndStaysOnTheChoice()
        {
            SessionFlowRig rig = new();
            rig.Begin(SessionFlowFixtures.RouteFirstSystem);

            CommandResult result = rig.Flow.Execute(new SelectRoute(new RouteID("WB-ROUTE-999")));

            Assert.AreEqual(CommandRejection.DomainRejected, result.Rejection);
            Assert.AreEqual(SessionStage.RouteSelection, result.Stage);
            Assert.AreEqual(1, rig.StageChanges.Count);
        }

        [Test]
        public void PurchaseOffer_AnInstruction_IsAcceptedAddsItAndSpendsItsPrice()
        {
            int tokens = Tokens();
            int entries = _session.Repository.Entries.Count;

            CommandResult result = _rig.Flow.Execute(new PurchaseOffer(1));

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(((PurchaseResult)result.DomainResult).Succeeded);
            Assert.AreEqual(entries + 1, _session.Repository.Entries.Count);
            Assert.AreEqual(tokens - 1, Tokens());
        }

        [Test]
        public void PurchaseOffer_APatchSlot_IsRefusedAsNotARoutinePurchaseAndChangesNothing()
        {
            int tokens = Tokens();
            int entries = _session.Repository.Entries.Count;

            CommandResult result = _rig.Flow.Execute(new PurchaseOffer(3));

            Assert.AreEqual(ShopRejection.NotARoutinePurchase, ((PurchaseResult)result.DomainResult).Rejection);
            Assert.AreEqual(tokens, Tokens());
            Assert.AreEqual(entries, _session.Repository.Entries.Count);
        }

        [Test]
        public void PurchaseOffer_AUtility_CommitsItToTheNextProcess()
        {
            CommandResult result = _rig.Flow.Execute(new PurchaseOffer(2));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, _session.Economy.Utilities.Count);
            Assert.AreEqual(SessionFlowPlayContent.PlayLast, ((PurchaseResult)result.DomainResult).Committed.TargetProcess);
        }

        [Test]
        public void DeleteItem_ABoughtItem_RemovesItAndSpendsTokens()
        {
            PurchaseResult bought = (PurchaseResult)_rig.Flow.Execute(new PurchaseOffer(1)).DomainResult;
            int tokens = Tokens();
            int entries = _session.Repository.Entries.Count;

            CommandResult result = _rig.Flow.Execute(new DeleteItem(bought.Acquired.Item.InstanceID));

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(((ServiceResult)result.DomainResult).Succeeded);
            Assert.AreEqual(entries - 1, _session.Repository.Entries.Count);
            Assert.Less(Tokens(), tokens);
        }

        [Test]
        public void DeleteItem_AnUnknownItem_IsRefusedAndChangesNothing()
        {
            int tokens = Tokens();
            int entries = _session.Repository.Entries.Count;

            CommandResult result = _rig.Flow.Execute(new DeleteItem(new InstanceID(999)));

            Assert.AreEqual(ServiceRejection.UnknownInstance, ((ServiceResult)result.DomainResult).Rejection);
            Assert.AreEqual(tokens, Tokens());
            Assert.AreEqual(entries, _session.Repository.Entries.Count);
        }

        [Test]
        public void DuplicateItem_AnOwnedItem_AddsACopyAndSpendsTokens()
        {
            int tokens = Tokens();
            int copies = _session.Repository.EntriesOf(SessionFlowFixtures.Structure).Count;

            CommandResult result = _rig.Flow.Execute(new DuplicateItem(Item(SessionFlowFixtures.Structure)));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(copies + 1, _session.Repository.EntriesOf(SessionFlowFixtures.Structure).Count);
            Assert.Less(Tokens(), tokens);
        }

        [Test]
        public void DuplicateItem_AnUnknownItem_IsRefused()
        {
            CommandResult result = _rig.Flow.Execute(new DuplicateItem(new InstanceID(999)));

            Assert.AreEqual(ServiceRejection.UnknownInstance, ((ServiceResult)result.DomainResult).Rejection);
        }

        [Test]
        public void PinOffer_PinThenUnpin_AreBothAccepted()
        {
            CommandResult pinned = _rig.Flow.Execute(new PinOffer(1, true));
            CommandResult unpinned = _rig.Flow.Execute(new PinOffer(1, false));

            Assert.IsTrue(((ShopActionResult)pinned.DomainResult).Succeeded);
            Assert.IsTrue(((ShopActionResult)unpinned.DomainResult).Succeeded);
        }

        [Test]
        public void PinOffer_AnUnknownSlot_IsRefused()
        {
            CommandResult result = _rig.Flow.Execute(new PinOffer(9, true));

            Assert.AreEqual(ShopRejection.UnknownSlot, ((ShopActionResult)result.DomainResult).Rejection);
        }

        [Test]
        public void RerollShop_KeepsAPinnedSlotAndSpendsTokens()
        {
            _rig.Flow.Execute(new PinOffer(1, true));
            int tokens = Tokens();

            CommandResult result = _rig.Flow.Execute(new RerollShop());

            RerollResult rerolled = (RerollResult)result.DomainResult;
            Assert.IsTrue(rerolled.Succeeded);
            CollectionAssert.DoesNotContain(rerolled.ReplacedSlots, 1);
            Assert.Less(Tokens(), tokens);
        }

        [Test]
        public void InstallPatch_FromTheShop_AttachesThePatchToTheHost()
        {
            InstanceID host = Item(SessionFlowFixtures.Instruction);

            CommandResult result = _rig.Flow.Execute(new InstallPatch(3, null, host, 1));

            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(((PatchAttachmentResult)result.DomainResult).Succeeded);
        }

        [Test]
        public void InstallPatch_AnOutOfRangeSocket_IsRefusedAndSpendsNothing()
        {
            int tokens = Tokens();

            CommandResult result = _rig.Flow.Execute(new InstallPatch(3, null, Item(SessionFlowFixtures.Instruction), 9));

            Assert.AreEqual(PatchAttachmentRejection.SocketOutOfRange, ((PatchAttachmentResult)result.DomainResult).Rejection);
            Assert.AreEqual(tokens, Tokens());
        }

        [Test]
        public void InstallPatch_InTheShopWithAGrant_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _rig.Flow.Execute(new InstallPatch(null, new InstanceID(1), Item(SessionFlowFixtures.Instruction), 1)));
        }

        [Test]
        public void InstallDependency_TheDependencyOffer_InstallsIt()
        {
            int installed = _session.Economy.Dependencies.AllInstalled.Count;

            CommandResult result = _rig.Flow.Execute(new InstallDependency(4, Array.Empty<InstanceID>()));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(installed + 1, _session.Economy.Dependencies.AllInstalled.Count);
        }

        [Test]
        public void InstallDependency_ANonDependencySlot_IsRefused()
        {
            int installed = _session.Economy.Dependencies.AllInstalled.Count;

            CommandResult result = _rig.Flow.Execute(new InstallDependency(1, Array.Empty<InstanceID>()));

            Assert.AreEqual(CommandRejection.DomainRejected, result.Rejection);
            Assert.AreEqual(installed, _session.Economy.Dependencies.AllInstalled.Count);
        }

        [Test]
        public void DestroyDependency_ABoughtDependency_IsAccepted()
        {
            DependencyInstallationResult bought = (DependencyInstallationResult)_rig.Flow.Execute(new InstallDependency(4, Array.Empty<InstanceID>())).DomainResult;
            int installed = _session.Economy.Dependencies.AllInstalled.Count;

            CommandResult result = _rig.Flow.Execute(new DestroyDependency(bought.Installed.Instance.InstanceID));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(installed - 1, _session.Economy.Dependencies.AllInstalled.Count);
        }

        [Test]
        public void DestroyDependency_TheStarter_IsRefusedAsPermanent()
        {
            InstanceID starter = _session.Economy.Dependencies.AllInstalled[0].InstanceID;

            CommandResult result = _rig.Flow.Execute(new DestroyDependency(starter));

            Assert.AreEqual(DependencyRejection.StarterPermanent, ((DependencyDestructionResult)result.DomainResult).Rejection);
        }

        [Test]
        public void LeaveShop_ClosesTheShopAndEntersTheNextProcess()
        {
            CommandResult result = _rig.Flow.Execute(new LeaveShop());

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(SessionStage.Build, result.Stage);
            Assert.AreEqual(SessionFlowPlayContent.PlayLast.Value, _rig.Scopes.Process.ProcessIdentity);
            Assert.AreEqual(CommandRejection.WrongStage, _rig.Flow.Execute(new RerollShop()).Rejection);
        }

        [Test]
        public void LeaveShop_RightAfterAPatchPurchase_IsAccepted()
        {
            Assert.IsTrue(_rig.Flow.Execute(new InstallPatch(3, null, Item(SessionFlowFixtures.Instruction), 1)).Accepted);

            Assert.IsTrue(_rig.Flow.Execute(new LeaveShop()).Accepted);
        }

        [Test]
        public void LeaveShop_RightAfterADependencyPurchase_IsAccepted()
        {
            Assert.IsTrue(_rig.Flow.Execute(new InstallDependency(4, Array.Empty<InstanceID>())).Accepted);

            Assert.IsTrue(_rig.Flow.Execute(new LeaveShop()).Accepted);
        }

        /// <summary>
        /// The first Repository entry of a definition.
        /// </summary>
        /// <param name="definition">The content ID.</param>
        /// <returns>The instance identity.</returns>
        private InstanceID Item(string definition)
        {
            return _session.Repository.EntriesOf(definition)[0].Item.InstanceID;
        }

        /// <summary>
        /// The Session's Token balance.
        /// </summary>
        /// <returns>The balance.</returns>
        private int Tokens()
        {
            return _session.Economy.Tokens.Balance.Value;
        }
    }
}
