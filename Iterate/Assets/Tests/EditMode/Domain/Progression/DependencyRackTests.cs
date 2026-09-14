using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests the Dependency rack: RAM as a reserved capacity rather than a counter, the starter
    /// Dependency as permanently installed and outside both the capacity and the removable set, and
    /// capacity raises that leave usage untouched. Every rejection leaves the rack exactly as it was,
    /// which is what lets an installer validate a whole composite before committing any of it.
    /// </summary>
    public sealed class DependencyRackTests
    {
        private static DependencyRack Rack(int capacity)
        {
            return new DependencyRack(Starter(), capacity);
        }

        private static DependencyInstance Starter()
        {
            return EconomyFixtures.Instance("WB-DEP-001", 0, 1);
        }

        [Test]
        public void NewRack_OpensAtItsCapacityWithTheStarterInstalled()
        {
            DependencyRack rack = Rack(4);

            Assert.AreEqual(4, rack.Capacity);
            Assert.AreEqual(0, rack.Usage);
            Assert.AreEqual(4, rack.Free);
            Assert.AreEqual(1, rack.AllInstalled.Count);
            Assert.AreEqual(new InstanceID(1), rack.AllInstalled[0].InstanceID);
        }

        [Test]
        public void Install_ReservesItsRAMAndListsTheEntry()
        {
            DependencyRack rack = Rack(4);

            RackResult result = rack.Install(
                EconomyFixtures.Dependency(EconomyFixtures.OneRAMDependency, 1),
                new InstanceID(10),
                4,
                DependencyOrigin.Purchase);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, result.RAMChanged);
            Assert.AreEqual(1, rack.Usage);
            Assert.AreEqual(3, rack.Free);
            Assert.AreEqual(4, result.Entry.PricePaid);
            Assert.AreEqual(DependencyOrigin.Purchase, result.Entry.Origin);
        }

        [Test]
        public void AllInstalled_ListsTheStarterFirstThenInstallationOrder()
        {
            DependencyRack rack = Rack(4);
            rack.Install(EconomyFixtures.Dependency(EconomyFixtures.OneRAMDependency, 1), new InstanceID(10), 4, DependencyOrigin.Purchase);
            rack.Install(EconomyFixtures.Dependency(EconomyFixtures.TwoRAMDependency, 2), new InstanceID(11), 8, DependencyOrigin.Purchase);

            Assert.AreEqual(3, rack.AllInstalled.Count);
            Assert.AreEqual(new InstanceID(1), rack.AllInstalled[0].InstanceID);
            Assert.AreEqual(new InstanceID(10), rack.AllInstalled[1].InstanceID);
            Assert.AreEqual(new InstanceID(11), rack.AllInstalled[2].InstanceID);
        }

        [Test]
        public void Install_BeyondFreeRAM_RejectsInsufficientRAMAndChangesNothing()
        {
            DependencyRack rack = Rack(4);
            rack.Install(EconomyFixtures.Dependency(EconomyFixtures.OneRAMDependency, 1), new InstanceID(10), 4, DependencyOrigin.Purchase);
            rack.Install(EconomyFixtures.Dependency(EconomyFixtures.TwoRAMDependency, 2), new InstanceID(11), 8, DependencyOrigin.Purchase);
            rack.Install(EconomyFixtures.Dependency("WB-DEP-203", 1), new InstanceID(12), 4, DependencyOrigin.Purchase);

            RackResult result = rack.Install(
                EconomyFixtures.Dependency("WB-DEP-204", 1),
                new InstanceID(13),
                4,
                DependencyOrigin.Purchase);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(DependencyRejection.InsufficientRAM, result.Rejection);
            Assert.AreEqual(4, rack.Usage);
            Assert.AreEqual(4, rack.AllInstalled.Count);
        }

        [Test]
        public void Destroy_ReleasesItsRAMAndFreesTheSlotForReuse()
        {
            DependencyRack rack = Rack(4);
            rack.Install(EconomyFixtures.Dependency(EconomyFixtures.TwoRAMDependency, 2), new InstanceID(10), 8, DependencyOrigin.Purchase);

            RackResult destroyed = rack.Destroy(new InstanceID(10));

            Assert.IsTrue(destroyed.Succeeded);
            Assert.AreEqual(2, destroyed.RAMChanged);
            Assert.AreEqual(0, rack.Usage);
            Assert.IsTrue(rack.Install(EconomyFixtures.Dependency(EconomyFixtures.TwoRAMDependency, 2), new InstanceID(11), 8, DependencyOrigin.Purchase).Succeeded);
            Assert.AreEqual(2, rack.Usage);
        }

        [Test]
        public void Destroy_TheStarter_RejectsStarterPermanent()
        {
            DependencyRack rack = Rack(4);

            RackResult result = rack.Destroy(new InstanceID(1));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(DependencyRejection.StarterPermanent, result.Rejection);
            Assert.AreEqual(1, rack.AllInstalled.Count);
        }

        [Test]
        public void Destroy_AnUnknownInstance_RejectsUnknownInstance()
        {
            DependencyRack rack = Rack(4);

            RackResult result = rack.Destroy(new InstanceID(99));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(DependencyRejection.UnknownInstance, result.Rejection);
        }

        [Test]
        public void IsRemovable_IsFalseForTheStarterAndTrueForAPurchase()
        {
            DependencyRack rack = Rack(4);
            rack.Install(EconomyFixtures.Dependency(EconomyFixtures.OneRAMDependency, 1), new InstanceID(10), 4, DependencyOrigin.Purchase);

            Assert.IsFalse(rack.IsRemovable(new InstanceID(1)));
            Assert.IsTrue(rack.IsRemovable(new InstanceID(10)));
            Assert.IsFalse(rack.IsRemovable(new InstanceID(99)));
        }

        [Test]
        public void TryGet_FindsAnInstalledEntryAndMissesTheStarter()
        {
            DependencyRack rack = Rack(4);
            rack.Install(EconomyFixtures.Dependency(EconomyFixtures.OneRAMDependency, 1), new InstanceID(10), 6, DependencyOrigin.Purchase);

            Assert.IsTrue(rack.TryGet(new InstanceID(10), out InstalledDependency entry));
            Assert.AreEqual(6, entry.PricePaid);
            Assert.IsFalse(rack.TryGet(new InstanceID(1), out InstalledDependency _));
        }

        [Test]
        public void SetCapacity_BelowUsage_RejectsAndLeavesCapacityUnchanged()
        {
            DependencyRack rack = Rack(4);
            rack.Install(EconomyFixtures.Dependency(EconomyFixtures.TwoRAMDependency, 2), new InstanceID(10), 8, DependencyOrigin.Purchase);
            rack.Install(EconomyFixtures.Dependency(EconomyFixtures.TwoRAMDependency, 2), new InstanceID(11), 8, DependencyOrigin.Purchase);

            RackResult result = rack.SetCapacity(3);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(DependencyRejection.CapacityBelowUsage, result.Rejection);
            Assert.AreEqual(4, rack.Capacity);
        }

        [Test]
        public void SetCapacity_Raised_LeavesUsageAndWidensFreeRAM()
        {
            DependencyRack rack = Rack(4);
            rack.Install(EconomyFixtures.Dependency(EconomyFixtures.TwoRAMDependency, 2), new InstanceID(10), 8, DependencyOrigin.Purchase);

            RackResult result = rack.SetCapacity(5);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(5, rack.Capacity);
            Assert.AreEqual(2, rack.Usage);
            Assert.AreEqual(3, rack.Free);
        }

        [Test]
        public void SetCapacity_ExactlyUsage_IsAllowed()
        {
            DependencyRack rack = Rack(4);
            rack.Install(EconomyFixtures.Dependency(EconomyFixtures.TwoRAMDependency, 2), new InstanceID(10), 8, DependencyOrigin.Purchase);

            Assert.IsTrue(rack.SetCapacity(2).Succeeded);
            Assert.AreEqual(2, rack.Capacity);
            Assert.AreEqual(0, rack.Free);
        }

        [Test]
        public void Starter_DoesNotConsumeCapacity()
        {
            DependencyRack rack = Rack(4);

            Assert.AreEqual(0, rack.Usage);
            Assert.AreEqual(0, rack.Starter.Definition.RAM);
        }
    }
}
