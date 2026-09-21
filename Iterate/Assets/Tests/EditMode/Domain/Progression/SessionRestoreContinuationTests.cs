using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Determinism;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests that a Session restored from a snapshot continues as the uninterrupted Session would have:
    /// the same identities minted, the same deterministic decisions drawn, the same Branch selection and
    /// the same positions over a modified Branch. Two drivers run the same steps from the same point —
    /// one carrying on, one rebuilt from a snapshot — so the only difference between them is the restore.
    ///
    /// The capture moment is a contract rather than a convenience: the snapshot is taken after the Branch
    /// is confirmed and before the Process is created, because creating a Process with a Process rule
    /// allocates an identity from the Session's source. A snapshot taken after that would shift every
    /// later identity by one on the restored side, and the rule instance and next allocation assertions
    /// here are what would catch it.
    /// </summary>
    public sealed class SessionRestoreContinuationTests
    {
        private const string ShopIdentity = "WB-SHOP-101";
        private const string PoolIdentity = "WB-POOL-101";
        private const string TargetProcess = "WB-PROC-004";

        [Test]
        public void Restore_BeforeAShop_RerollsIdentically()
        {
            ContentCatalog catalog = ShopCatalog();
            SessionSequenceDriver carried = ShopDriver(catalog);
            SessionSequenceDriver restored = RestoredDriver(catalog, carried);

            RerollResult carriedRoll = Reroll(carried);
            RerollResult restoredRoll = Reroll(restored);

            Assert.IsTrue(carriedRoll.Succeeded);
            Assert.IsTrue(restoredRoll.Succeeded);
            Assert.AreEqual(carriedRoll.Decision, restoredRoll.Decision, "one seed, one decision.");
            CollectionAssert.AreEqual(Contents(carried.Shop), Contents(restored.Shop));
        }

        [Test]
        public void Restore_BeforeAShop_ConsumesTheSameSessionOrdinal()
        {
            // The shop reroll is the only Session-scoped ordinal consumer today, so this is the one place
            // a lost ordinal would silently change a decision context rather than fail loudly.
            ContentCatalog catalog = ShopCatalog();
            SessionSequenceDriver carried = ShopDriver(catalog);
            SessionSequenceDriver restored = RestoredDriver(catalog, carried);

            Reroll(carried);
            Reroll(restored);

            CollectionAssert.AreEqual(carried.Session.Ordinals.Snapshot(), restored.Session.Ordinals.Snapshot());
            Assert.AreEqual(
                carried.Session.Ordinals.Next(ShopState.Purpose, ShopIdentity),
                restored.Session.Ordinals.Next(ShopState.Purpose, ShopIdentity)
            );
        }

        [Test]
        public void Restore_BeforeAShop_MintsTheSameIdentitiesAndTransactionIdentityOnAPurchase()
        {
            ContentCatalog catalog = ShopCatalog();
            SessionSequenceDriver carried = ShopDriver(catalog);
            SessionSequenceDriver restored = RestoredDriver(catalog, carried);
            carried.OpenShop(new ShopID(ShopIdentity), new ProcessID(TargetProcess));
            restored.OpenShop(new ShopID(ShopIdentity), new ProcessID(TargetProcess));

            PurchaseResult carriedBuy = carried.Shop.Purchase(1);
            PurchaseResult restoredBuy = restored.Shop.Purchase(1);

            Assert.IsTrue(carriedBuy.Succeeded);
            Assert.IsTrue(restoredBuy.Succeeded);
            Assert.AreEqual(carriedBuy.Acquired.Item.InstanceID, restoredBuy.Acquired.Item.InstanceID);
            Assert.AreEqual(carriedBuy.Acquired.Suffix, restoredBuy.Acquired.Suffix);
            Assert.AreEqual(carriedBuy.Record.TransactionIdentity, restoredBuy.Record.TransactionIdentity);
            Assert.AreEqual(carried.Session.InstanceIDs.Next(), restored.Session.InstanceIDs.Next());
        }

        [Test]
        public void Restore_AtAProcessEntry_SelectsTheSameExposedContent()
        {
            ContentCatalog catalog = ProgressionFixtures.ProcessCatalog();
            ProcessConfigurationDefinition configuration = Configuration();
            SessionSequenceDriver carried = ConfirmedDriver(catalog, configuration);
            SessionSequenceDriver restored = RestoredDriver(catalog, carried);

            ProcessCreationResult carriedProcess = carried.CreateProcess(configuration);
            ProcessCreationResult restoredProcess = restored.CreateProcess(configuration);

            Assert.IsTrue(carriedProcess.Succeeded);
            Assert.IsTrue(restoredProcess.Succeeded);
            CollectionAssert.AreEqual(carried.Process.ExposedInstanceIDs, restored.Process.ExposedInstanceIDs);
            CollectionAssert.AreEqual(carried.Process.ExposureRecords, restored.Process.ExposureRecords);
        }

        [Test]
        public void Restore_AtAProcessEntry_MintsTheSameProcessRuleIdentityAndNextAllocation()
        {
            // The capture-moment contract, asserted directly: ProcessState.Create allocates the Process
            // rule's identity from the Session's source, so a snapshot taken after it would leave the
            // restored side one behind for the rest of the Session.
            ContentCatalog catalog = ProgressionFixtures.ProcessCatalog();
            ProcessConfigurationDefinition configuration = Configuration();
            SessionSequenceDriver carried = ConfirmedDriver(catalog, configuration);
            SessionSequenceDriver restored = RestoredDriver(catalog, carried);

            carried.CreateProcess(configuration);
            restored.CreateProcess(configuration);

            Assert.IsNotNull(carried.Process.ProcessRule, "the configuration must name a Process rule, or this proves nothing.");
            Assert.AreEqual(carried.Process.ProcessRule.InstanceID, restored.Process.ProcessRule.InstanceID);
            Assert.AreEqual(carried.Session.InstanceIDs.Next(), restored.Session.InstanceIDs.Next());
        }

        [Test]
        public void Restore_AtAProcessEntry_ReproducesPositionsOverAModifiedBranch()
        {
            ContentCatalog catalog = ProgressionFixtures.ProcessCatalog();
            ProcessConfigurationDefinition configuration = Configuration();
            SessionSequenceDriver carried = ConfirmedDriver(catalog, configuration);
            SessionSequenceDriver restored = RestoredDriver(catalog, carried);

            InstanceID dropped = carried.Session.ActiveBranch.Confirmed.Eligible[0];
            Reconfirm(carried, configuration, dropped);
            Reconfirm(restored, configuration, dropped);
            carried.CreateProcess(configuration);
            restored.CreateProcess(configuration);

            CollectionAssert.AreEqual(
                carried.Session.ActiveBranch.Confirmed.Eligible,
                restored.Session.ActiveBranch.Confirmed.Eligible
            );
            CollectionAssert.AreEqual(carried.Process.ExposedInstanceIDs, restored.Process.ExposedInstanceIDs);
        }

        [Test]
        public void Restore_AtAProcessEntry_CarriesTheConfirmedBranchThroughTheSnapshot()
        {
            ContentCatalog catalog = ProgressionFixtures.ProcessCatalog();
            SessionSequenceDriver carried = ConfirmedDriver(catalog, Configuration());

            SessionSequenceDriver restored = RestoredDriver(catalog, carried);

            Assert.AreEqual(carried.Session.ActiveBranch.Confirmed, restored.Session.ActiveBranch.Confirmed);
            Assert.IsFalse(restored.Session.ActiveBranch.HasConfigurationInProgress);
        }

        /// <summary>
        /// Builds a driver over a restored copy of another driver's Session, taken at the point the
        /// source driver has reached.
        /// </summary>
        /// <param name="catalog">The catalog both drivers share.</param>
        /// <param name="source">The driver whose Session is snapshotted.</param>
        /// <returns>The driver over the restored Session.</returns>
        private static SessionSequenceDriver RestoredDriver(ContentCatalog catalog, SessionSequenceDriver source)
        {
            SessionRestoreResult result = SessionState.Restore(catalog, source.Session.Snapshot());

            Assert.IsTrue(result.Succeeded, "the snapshot under test must restore, or the comparison is vacuous.");
            return new SessionSequenceDriver(catalog, result.Session);
        }

        /// <summary>
        /// Builds a driver over the shop catalog with Tokens enough for a reroll and a purchase.
        /// </summary>
        /// <param name="catalog">The shop catalog.</param>
        /// <returns>The driver.</returns>
        private static SessionSequenceDriver ShopDriver(ContentCatalog catalog)
        {
            SessionSequenceDriver driver = new(
                catalog,
                ProgressionFixtures.StandardArchetype(),
                "WB-SYS-001",
                "session-1",
                "seed-1"
            );
            driver.Session.Economy.Tokens.Credit(new TokenAmount(9), TokenBasis.RewardTokens, "fixture");
            return driver;
        }

        /// <summary>
        /// Opens the fixture shop on a driver and rerolls it once.
        /// </summary>
        /// <param name="driver">The driver to act on.</param>
        /// <returns>The reroll result.</returns>
        private static RerollResult Reroll(SessionSequenceDriver driver)
        {
            driver.OpenShop(new ShopID(ShopIdentity), new ProcessID(TargetProcess));
            return driver.Shop.Reroll();
        }

        /// <summary>
        /// Builds a driver taken to a Process entry: a Branch configured, trimmed to capacity and
        /// confirmed, with no Process yet created.
        /// </summary>
        /// <param name="catalog">The catalog to seed from.</param>
        /// <param name="configuration">The Process whose constraints the Branch is judged against.</param>
        /// <returns>The driver at the capture moment.</returns>
        private static SessionSequenceDriver ConfirmedDriver(
            ContentCatalog catalog,
            ProcessConfigurationDefinition configuration
        )
        {
            SessionSequenceDriver driver = new(
                catalog,
                ProgressionFixtures.StandardArchetype(),
                "WB-SYS-001",
                "session-1",
                "seed-1"
            );
            driver.ConfigureBranch(configuration);
            driver.TrimToCapacity();

            Assert.IsTrue(driver.ConfirmBranch().Succeeded, "the fixture Branch must confirm, or the capture moment is not reached.");
            return driver;
        }

        /// <summary>
        /// Reopens the Branch, excludes one instance and confirms again.
        /// </summary>
        /// <param name="driver">The driver to act on.</param>
        /// <param name="configuration">The Process whose constraints apply.</param>
        /// <param name="excluded">The instance to drop from the Branch.</param>
        private static void Reconfirm(
            SessionSequenceDriver driver,
            ProcessConfigurationDefinition configuration,
            InstanceID excluded
        )
        {
            driver.ConfigureBranch(configuration);
            driver.Session.ActiveBranch.Exclude(excluded);
            driver.ConfirmBranch();
        }

        /// <summary>
        /// The content identity in each of a shop's slots, in slot order.
        /// </summary>
        /// <param name="shop">The open shop.</param>
        /// <returns>The content identities.</returns>
        private static IReadOnlyList<string> Contents(ShopState shop)
        {
            List<string> contents = new(shop.Slots.Count);
            for (int index = 0; index < shop.Slots.Count; index++)
            {
                contents.Add(shop.Slots[index].Offer.ContentID);
            }

            return contents;
        }

        /// <summary>
        /// Builds a three-slot shop with a reroll pool over content the shop does not already show.
        /// </summary>
        /// <returns>The catalog.</returns>
        private static ContentCatalog ShopCatalog()
        {
            ShopDefinition shop = EconomyFixtures.Shop(
                ShopIdentity,
                3,
                true,
                true,
                false,
                false,
                new[]
                {
                    EconomyFixtures.Offer("OFF-901", ProgressionFixtures.ValuePlusTwo, 4),
                    EconomyFixtures.Offer("OFF-902", ProgressionFixtures.ValuePlusThree, 4),
                    EconomyFixtures.Offer("OFF-903", EconomyFixtures.StartingBytesUtility, 3)
                },
                new PoolID(PoolIdentity)
            );

            PoolDefinition pool = EconomyFixtures.Pool(
                PoolIdentity,
                4,
                ProgressionFixtures.ScorePlusValue,
                ProgressionFixtures.RepeatTwo,
                ProgressionFixtures.ConditionValueEven,
                ProgressionFixtures.Overclock,
                EconomyFixtures.OneRAMDependency,
                EconomyFixtures.TwoRAMDependency
            );

            return EconomyFixtures.ShopCatalog(new[] { shop }, new[] { pool });
        }

        /// <summary>
        /// Builds the Process-3-shaped configuration both sides create: a Branch to confirm, a drawn
        /// Buffer load with exposure, and a Process rule whose instance identity proves the capture
        /// moment.
        /// </summary>
        /// <returns>The configuration.</returns>
        private static ProcessConfigurationDefinition Configuration()
        {
            BufferLoadSpec load = new(
                BufferLoadPolicy.Drawn,
                Array.Empty<string>(),
                Array.Empty<ArrivalLoadSpec>(),
                2,
                new[] { 1, 2, 3 }
            );

            return new ProcessConfigurationDefinition(
                new ProcessID("WB-PROC-003"),
                "Process 3",
                ProcessRole.RouteProcess3,
                new CoreID("WB-CORE-002"),
                new ProcessRuleID("WB-PRC-002"),
                new ProcessThresholdSpec(18, 32, 80),
                4,
                false,
                6,
                8,
                5,
                Array.Empty<InitialSourceSpec>(),
                load,
                new ExposureSpec(new[] { ProgressionFixtures.ScorePlusValue }),
                // The capacity holds the whole starter Repository and the guaranteed content is Required,
                // because the exposure spec below guarantees ScorePlusValue and the draw resolves it from
                // the Branch: a capacity that forced a trim would drop the highest identity, which is
                // that very instance, and the Process would refuse to open with GuaranteedContentMissing.
                new ActiveBranchSpec(
                    3,
                    new[] { ProgressionFixtures.ScorePlusValue },
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>()
                ),
                new RewardPackageID("WB-RWD-003"),
                null
            );
        }
    }
}
