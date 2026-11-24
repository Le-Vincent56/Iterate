using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Application.SessionFlow;
using Iterate.Domain.Compilation;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow.Tests
{
    /// <summary>
    /// Tests the twenty-three command records: each carries identities and primitives only, and its members
    /// read back what it was built with. The two commands holding a list reject a null list at
    /// construction, so a handler never has to tell "no instances" from "no list". Those two are
    /// compared member by member, because a list member makes the generated equality a reference
    /// comparison.
    /// </summary>
    public sealed class CommandRecordTests
    {
        private static readonly InstanceID _item = new InstanceID(3);
        private static readonly InstanceID _other = new InstanceID(7);

        [Test]
        public void InstallLine_RoundTripsItemAndPosition()
        {
            InstallLine command = new InstallLine(_item, new SourcePosition(2));

            Assert.AreEqual(_item, command.Item);
            Assert.AreEqual(new SourcePosition(2), command.Position);
        }

        [Test]
        public void RemoveLine_RoundTripsPosition()
        {
            Assert.AreEqual(new SourcePosition(4), new RemoveLine(new SourcePosition(4)).Position);
        }

        [Test]
        public void MoveLine_RoundTripsOriginAndDestination()
        {
            MoveLine command = new MoveLine(new SourcePosition(1), new SourcePosition(5));

            Assert.AreEqual(new SourcePosition(1), command.Origin);
            Assert.AreEqual(new SourcePosition(5), command.Destination);
        }

        [Test]
        public void SwapLines_RoundTripsBothPositions()
        {
            SwapLines command = new SwapLines(new SourcePosition(2), new SourcePosition(3));

            Assert.AreEqual(new SourcePosition(2), command.First);
            Assert.AreEqual(new SourcePosition(3), command.Second);
        }

        [Test]
        public void ArchiveItem_RoundTripsAnItemOrNullForTheIncomingItem()
        {
            Assert.AreEqual(_item, new ArchiveItem(_item).Item);
            Assert.IsNull(new ArchiveItem(null).Item);
        }

        [Test]
        public void ConfirmDirective_RoundTripsDirective()
        {
            Assert.AreEqual(_item, new ConfirmDirective(_item).Directive);
        }

        [Test]
        public void PurchaseOffer_RoundTripsSlot()
        {
            Assert.AreEqual(2, new PurchaseOffer(2).Slot);
        }

        [Test]
        public void DeleteItemAndDuplicateItem_RoundTripTheItem()
        {
            Assert.AreEqual(_item, new DeleteItem(_item).Item);
            Assert.AreEqual(_other, new DuplicateItem(_other).Item);
        }

        [Test]
        public void PinOffer_RoundTripsSlotAndPinned()
        {
            PinOffer command = new PinOffer(1, true);

            Assert.AreEqual(1, command.Slot);
            Assert.IsTrue(command.Pinned);
            Assert.IsFalse(new PinOffer(1, false).Pinned);
        }

        [Test]
        public void InstallPatch_FromAShopSlot_RoundTripsSlotHostAndSocket()
        {
            InstallPatch command = new InstallPatch(3, null, _item, 1);

            Assert.AreEqual(3, command.Slot);
            Assert.IsNull(command.Grant);
            Assert.AreEqual(_item, command.Host);
            Assert.AreEqual(1, command.Socket);
        }

        [Test]
        public void InstallPatch_FromARewardGrant_RoundTripsGrantHostAndSocket()
        {
            InstallPatch command = new InstallPatch(null, _other, _item, 0);

            Assert.IsNull(command.Slot);
            Assert.AreEqual(_other, command.Grant);
            Assert.AreEqual(_item, command.Host);
            Assert.AreEqual(0, command.Socket);
        }

        [Test]
        public void InstallDependency_RoundTripsSlotAndDestroyFirst()
        {
            List<InstanceID> destroyFirst = new List<InstanceID> { _item, _other };

            InstallDependency command = new InstallDependency(4, destroyFirst);

            Assert.AreEqual(4, command.Slot);
            CollectionAssert.AreEqual(destroyFirst, command.DestroyFirst);
        }

        [Test]
        public void InstallDependency_EmptyDestroyFirst_IsAllowed()
        {
            Assert.AreEqual(0, new InstallDependency(0, Array.Empty<InstanceID>()).DestroyFirst.Count);
        }

        [Test]
        public void InstallDependency_NullDestroyFirst_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new InstallDependency(0, null));
        }

        [Test]
        public void DestroyDependency_RoundTripsDependency()
        {
            Assert.AreEqual(_item, new DestroyDependency(_item).Dependency);
        }

        [TestCase(BranchAction.Include)]
        [TestCase(BranchAction.Exclude)]
        public void ConfigureBranch_ItemAction_RoundTripsActionAndItem(BranchAction action)
        {
            ConfigureBranch command = new ConfigureBranch(action, _item);

            Assert.AreEqual(action, command.Action);
            Assert.AreEqual(_item, command.Item);
        }

        [Test]
        public void ConfigureBranch_Confirm_CarriesNoItem()
        {
            ConfigureBranch command = new ConfigureBranch(BranchAction.Confirm, null);

            Assert.AreEqual(BranchAction.Confirm, command.Action);
            Assert.IsNull(command.Item);
        }

        [Test]
        public void SelectRoute_RoundTripsRoute()
        {
            Assert.AreEqual(new RouteID("WB-ROUTE-001"), new SelectRoute(new RouteID("WB-ROUTE-001")).Route);
        }

        [Test]
        public void SelectReward_RoundTripsComponentContentAndDestroyFirst()
        {
            List<InstanceID> destroyFirst = new List<InstanceID> { _other };

            SelectReward command = new SelectReward(1, "WB-DEP-002", destroyFirst);

            Assert.AreEqual(1, command.ComponentIndex);
            Assert.AreEqual("WB-DEP-002", command.ContentID);
            CollectionAssert.AreEqual(destroyFirst, command.DestroyFirst);
        }

        [Test]
        public void SelectReward_NullDestroyFirst_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new SelectReward(0, "WB-DEP-002", null));
        }

        [Test]
        public void MemberlessCommands_AreSessionCommandsWithValueEquality()
        {
            Assert.AreEqual(new CompileAndExecute(), new CompileAndExecute());
            Assert.AreEqual(new Submit(), new Submit());
            Assert.AreEqual(new Continue(), new Continue());
            Assert.AreEqual(new ReturnToBuild(), new ReturnToBuild());
            Assert.AreEqual(new RerollShop(), new RerollShop());
            Assert.AreEqual(new AbandonSession(), new AbandonSession());
            Assert.AreEqual(new LeaveShop(), new LeaveShop());
        }

        [Test]
        public void EveryCommandType_IsASealedSessionCommand()
        {
            foreach (Type command in SessionCommandRoutes.Commands)
            {
                Assert.IsTrue(typeof(ISessionCommand).IsAssignableFrom(command), command.Name);
                Assert.IsTrue(command.IsSealed, $"{command.Name} must be sealed.");
                Assert.IsTrue(command.IsClass, $"{command.Name} must be a record class.");
            }
        }
    }
}
