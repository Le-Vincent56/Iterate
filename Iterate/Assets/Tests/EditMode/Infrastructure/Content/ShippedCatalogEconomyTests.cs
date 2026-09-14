using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Iterate.Application.Content;
using Iterate.Application.Content.Json;
using Iterate.Domain.Content;
using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Infrastructure.Content.Tests
{
    /// <summary>
    /// Plays the slice's economy over the shipped catalog: the shops each Process precedes, the reward
    /// packages they award, the Services and Dependency transactions, and GARBAGE COLLECTOR's archive
    /// gain. Hand-built fixtures prove the transactions are correct; this proves the authored content
    /// they run over is, which is where a price band, an offer identity or a pool membership that no
    /// longer lines up actually surfaces.
    ///
    /// PARITY CHECK (WB-PROC-003) is deliberately not driven: backlog item 8 has not started, and the
    /// INV-10 precedent is that POWER SURGE is what the Process 3 scenarios mean.
    /// </summary>
    public sealed class ShippedCatalogEconomyTests
    {
        private ContentCatalog _catalog;

        [OneTimeSetUp]
        public void LoadCatalog()
        {
            _catalog = Load();
        }

        [Test]
        public void TutorialOneShop_SellsItsThreeFixedOffersAtTheirAuthoredPrices()
        {
            SessionState session = Session(4);
            ShopState shop = Open("WB-SHOP-001", "WB-PROC-002", session);

            Assert.AreEqual(3, shop.Slots.Count);
            Assert.AreEqual("WB-INS-001", shop.Slots[0].Offer.ContentID);
            Assert.AreEqual(4, shop.Slots[0].Offer.Price);
            Assert.AreEqual("OFF-001", shop.Slots[0].Offer.OfferIdentity);
            Assert.AreEqual(6, shop.Slots[2].Offer.Price);
        }

        [Test]
        public void TutorialOneShop_BuyingTheFirstOffer_SpendsTheWholeRewardAndTakesTheNextSuffix()
        {
            SessionState session = Session(4);
            ShopState shop = Open("WB-SHOP-001", "WB-PROC-002", session);

            PurchaseResult result = shop.Purchase(1);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
            Assert.AreEqual("WB-INS-001", result.Acquired.Item.DefinitionID);
            Assert.AreEqual(AcquisitionOrigin.Purchase, result.Acquired.Origin);
            // WB-ARCH-001 seeds WB-INS-002 twice and one each of 003, 005, 007, 010, 012, STR-001 and
            // DIR-001 — no WB-INS-001 — so the shop sells the Session its first, at suffix 1.
            Assert.AreEqual(1, result.Acquired.Suffix);
            Assert.AreEqual(1, session.Repository.EntriesOf("WB-INS-001").Count);
        }

        [Test]
        public void TutorialOneShop_OffersNoRerollAtAll()
        {
            SessionState session = Session(4);
            ShopState shop = Open("WB-SHOP-001", "WB-PROC-002", session);

            Assert.AreEqual(ShopRejection.RerollsDisabled, shop.Reroll().Rejection);
            Assert.IsNull(shop.NextRerollCost);
        }

        [Test]
        public void TutorialTwoRewards_AtBenchmark_CreditSixTokensAsTwoEntriesAndGrantAPatch()
        {
            SessionState session = Session(0);
            RewardResolution resolution = Award("WB-RWD-002", RewardTier.Benchmark, session);

            Assert.AreEqual(new TokenAmount(6), session.Economy.Tokens.Balance);
            Assert.AreEqual(2, session.Economy.Tokens.Entries.Count);
            Assert.AreEqual(TokenBasis.RewardTokens, session.Economy.Tokens.Entries[0].Basis);
            Assert.AreEqual(TokenBasis.BenchmarkBonus, session.Economy.Tokens.Entries[1].Basis);
            Assert.AreEqual(1, session.Economy.PatchGrants.Count);
            Assert.AreEqual("WB-PAT-002", session.Economy.PatchGrants[0].Definition.ID.Value);
            Assert.IsFalse(session.Economy.PatchGrants[0].Cached);
            Assert.IsFalse(resolution.IsComplete, "an uncached Patch grant holds the reward open");
        }

        [Test]
        public void TutorialTwoRewards_AtPass_AcquireTheGuaranteedStructure()
        {
            SessionState session = Session(0);
            Award("WB-RWD-002", RewardTier.Pass, session);

            RepositoryEntry acquired = session.Repository.Entries[session.Repository.Entries.Count - 1];

            Assert.AreEqual("WB-STR-002", acquired.Item.DefinitionID);
            Assert.AreEqual(AcquisitionOrigin.Reward, acquired.Origin);
        }

        [Test]
        public void TutorialOneRewards_AtOptimize_GrantACachedPatchThatDoesNotHoldTheRewardOpen()
        {
            SessionState session = Session(0);
            RewardResolution resolution = Award("WB-RWD-001", RewardTier.Optimize, session);

            Assert.AreEqual("WB-PAT-001", session.Economy.PatchGrants[0].Definition.ID.Value);
            Assert.IsTrue(session.Economy.PatchGrants[0].Cached);
            Assert.AreEqual(1, resolution.Pending.Count, "the Pass pool choice is still owed");

            resolution.Choose(1, "WB-INS-004", Array.Empty<InstanceID>());

            Assert.IsTrue(resolution.IsComplete);
        }

        [Test]
        public void PowerSurgeShop_PinsOneSlotAndRerollsTheRestOnceForOneToken()
        {
            SessionState session = Session(6);
            ShopState shop = Open("WB-SHOP-003", "WB-PROC-004", session);
            Assert.AreEqual("WB-UTL-001", shop.Slots[3].Offer.ContentID);
            shop.Pin(4);
            List<string> before = Contents(shop);

            RerollResult result = shop.Reroll();

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(new TokenAmount(5), session.Economy.Tokens.Balance);
            Assert.AreEqual(3, result.ReplacedSlots.Count);
            Assert.AreEqual("WB-UTL-001", shop.Slots[3].Offer.ContentID);
            for (int i = 0; i < 3; i++)
            {
                Assert.IsFalse(Holds(before, shop.Slots[i].Offer.ContentID), "a reroll never repeats shown content");
            }
        }

        [Test]
        public void PowerSurgeShop_BuyingTheUtility_CommitsItToPowerSurgeOnly()
        {
            SessionState session = Session(3);
            ShopState shop = Open("WB-SHOP-003", "WB-PROC-004", session);

            PurchaseResult result = shop.Purchase(4);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("WB-UTL-001", result.Committed.Definition.ID.Value);
            Assert.AreEqual(1, session.Economy.SetupEffectsFor(new ProcessID("WB-PROC-004")).Count);
            Assert.AreEqual(0, session.Economy.SetupEffectsFor(new ProcessID("WB-PROC-005")).Count);
            Assert.AreEqual(
                "STARTING_BYTES",
                session.Economy.SetupEffectsFor(new ProcessID("WB-PROC-004"))[0].Modification.Setting);
        }

        [Test]
        public void PowerSurgeShop_SellsAPatchIntoAnEmptySocketWithServicesDisabled()
        {
            SessionState session = Session(6);
            ShopState shop = Open("WB-SHOP-003", "WB-PROC-004", session);
            Assert.IsFalse(shop.Definition.ServicesEnabled);
            InstanceID host = OrdinaryHost(session);

            PatchAttachmentResult result = shop.PurchasePatch(3, host, 1);

            Assert.IsTrue(result.Succeeded, "WB-PAT-003 attaches to an ordinary Instruction host");
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
            Assert.AreEqual(ShopSlotState.Sold, shop.Slots[2].State);
        }

        [Test]
        public void PowerSurgeRewards_AtPass_CreditFourPlusTwoUnderDistinctBases()
        {
            SessionState session = Session(0);
            RewardResolution resolution = Award("WB-RWD-004", RewardTier.Pass, session);

            Assert.AreEqual(new TokenAmount(6), session.Economy.Tokens.Balance);
            Assert.AreEqual(2, session.Economy.Tokens.Entries.Count);
            Assert.AreEqual(TokenBasis.RewardTokens, session.Economy.Tokens.Entries[0].Basis);
            Assert.AreEqual(TokenBasis.RouteBonus, session.Economy.Tokens.Entries[1].Basis);
            Assert.AreEqual(1, resolution.Pending.Count);
        }

        [Test]
        public void PowerSurgeRewards_AtOptimize_GrantTheRoutePatch()
        {
            SessionState session = Session(0);
            Award("WB-RWD-004", RewardTier.Optimize, session);

            Assert.AreEqual("WB-PAT-005", session.Economy.PatchGrants[0].Definition.ID.Value);
        }

        [Test]
        public void PreCriticalShop_InstallsADependencyAndThenReplacesItFundedByItsRefund()
        {
            SessionState session = Session(4);
            ShopState shop = Open("WB-SHOP-004", "WB-PROC-005", session);

            DependencyInstallationResult first = shop.PurchaseDependency(1, Array.Empty<InstanceID>());

            Assert.IsTrue(first.Succeeded, "WB-DEP-002 costs 4 Tokens and 1 RAM");
            Assert.AreEqual(1, session.Economy.Dependencies.Usage);
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);

            session.Economy.Tokens.Credit(new TokenAmount(6), TokenBasis.RewardTokens, "next Process");
            DependencyInstallationResult second = shop.PurchaseDependency(
                3,
                new[] { first.Installed.Instance.InstanceID });

            Assert.IsTrue(second.Succeeded, "the 2-Token refund funds the 8-Token purchase");
            Assert.AreEqual(2, session.Economy.Dependencies.Usage);
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
        }

        [Test]
        public void PreCriticalShop_DestroyingADependencyRefundsHalfItsPriceAndFreesItsRAM()
        {
            SessionState session = Session(8);
            ShopState shop = Open("WB-SHOP-004", "WB-PROC-005", session);
            DependencyInstallationResult installed = shop.PurchaseDependency(3, Array.Empty<InstanceID>());
            Assert.AreEqual(2, session.Economy.Dependencies.Usage);

            DependencyDestructionResult destroyed = shop.DestroyDependency(installed.Installed.Instance.InstanceID);

            Assert.IsTrue(destroyed.Succeeded);
            Assert.AreEqual(4, destroyed.Refund);
            Assert.AreEqual(new TokenAmount(4), session.Economy.Tokens.Balance);
            Assert.AreEqual(0, session.Economy.Dependencies.Usage);
        }

        [Test]
        public void PreCriticalShop_RejectsADependencyOfferThroughTheRoutinePurchase()
        {
            SessionState session = Session(8);
            ShopState shop = Open("WB-SHOP-004", "WB-PROC-005", session);

            PurchaseResult result = shop.Purchase(1);

            Assert.AreEqual(ShopRejection.NotARoutinePurchase, result.Rejection);
            Assert.AreEqual(new TokenAmount(8), session.Economy.Tokens.Balance);
            Assert.AreEqual(ShopSlotState.Available, shop.Slots[0].State);
        }

        [Test]
        public void PreCriticalShop_OffersServicesWhereTheEarlierShopsDoNot()
        {
            SessionState session = Session(7);
            ShopState shop = Open("WB-SHOP-004", "WB-PROC-005", session);
            InstanceID item = session.Repository.Entries[0].Item.InstanceID;

            ServiceResult duplicated = shop.DuplicateItem(item);

            Assert.IsTrue(duplicated.Succeeded);
            Assert.AreEqual(AcquisitionOrigin.Duplicate, duplicated.Entry.Origin);
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
        }

        [Test]
        public void ThermalThrottleRewards_AtPass_RaiseRAMBeforeTheDependencyChoiceIsTaken()
        {
            SessionState session = Session(0);
            RewardResolution resolution = Award("WB-RWD-005", RewardTier.Pass, session);

            Assert.AreEqual(new TokenAmount(6), session.Economy.Tokens.Balance);
            Assert.AreEqual(5, session.Economy.Dependencies.Capacity, "RAM_SET applies before any choice");
            Assert.AreEqual(2, resolution.Pending.Count);

            RewardChoiceResult chosen = resolution.Choose(3, "WB-DEP-009", Array.Empty<InstanceID>());

            Assert.IsTrue(chosen.Succeeded);
            Assert.AreEqual(1, session.Economy.Dependencies.Usage);
            Assert.AreEqual(new TokenAmount(6), session.Economy.Tokens.Balance, "a rewarded Dependency costs nothing");
        }

        [Test]
        public void ThermalThrottleRewards_ADependencyChoiceOutsideItsPool_IsRejected()
        {
            SessionState session = Session(0);
            RewardResolution resolution = Award("WB-RWD-005", RewardTier.Pass, session);

            RewardChoiceResult chosen = resolution.Choose(3, "WB-DEP-002", Array.Empty<InstanceID>());

            Assert.AreEqual(RewardRejection.NotAPoolMember, chosen.Rejection);
            Assert.AreEqual(0, session.Economy.Dependencies.Usage);
        }

        [Test]
        public void GarbageCollector_IsAuthoredAsOneArchiveGainOfOneByte()
        {
            Assert.IsTrue(_catalog.TryGetDependency(new DependencyID("WB-DEP-008"), out DependencyDefinition dependency));

            IReadOnlyList<ArchiveObserver> observers = BuildInteractionEffects.Interpret(
                new DependencyInstance(new InstanceID(900), dependency));

            Assert.AreEqual(1, observers.Count);
            Assert.AreEqual("BYTES", observers[0].Gain.Resource);
            Assert.AreEqual(1, observers[0].Gain.Amount);
            Assert.AreEqual("FIRST_QUALIFYING_EVENT", observers[0].Frequency.Allowance);
            Assert.AreEqual("PROCESS", observers[0].Frequency.Scope);
        }

        [Test]
        public void GarbageCollector_IsReachableFromThePreCriticalShopsRerollPool()
        {
            Assert.IsTrue(_catalog.TryGetPool(new PoolID("WB-POOL-005"), out PoolDefinition pool));

            bool holds = false;
            for (int i = 0; i < pool.Members.Count; i++)
            {
                if (pool.Members[i].Content == "WB-DEP-008")
                    holds = true;
            }

            Assert.IsTrue(holds, "WB-SHOP-004 rerolls from WB-POOL-005, which is where a player meets it");
        }

        [Test]
        public void ParityCheck_IsNotDrivenByThisSuite()
        {
            // WB-PROC-003 is PARITY CHECK. Its route is backlog item 8, which has not started, so no
            // scenario here opens WB-SHOP-002 or awards WB-RWD-003: driving it would assert behaviour
            // this slice has not built. The INV-10 precedent is the same one — POWER SURGE is what the
            // Process 3 scenarios mean. This test states that absence rather than leaving it unsaid.
            Assert.IsTrue(_catalog.TryGetProcessConfiguration(new ProcessID("WB-PROC-003"), out ProcessConfigurationDefinition parity));
            Assert.AreEqual("PARITY CHECK", parity.DisplayName);
            Assert.AreEqual("WB-SHOP-002", parity.PrecedingShop.Value.Value);
        }

        private ShopState Open(string shop, string target, SessionState session)
        {
            Assert.IsTrue(_catalog.TryGetShop(new ShopID(shop), out ShopDefinition definition), shop);

            ShopOpenResult result = ShopState.Open(definition, _catalog, session, new ProcessID(target));

            Assert.IsTrue(result.Succeeded, shop + " did not open: " + result.Rejection);
            return result.Shop;
        }

        private RewardResolution Award(string package, RewardTier tier, SessionState session)
        {
            Assert.IsTrue(_catalog.TryGetRewardPackage(new RewardPackageID(package), out RewardPackageDefinition definition), package);

            RewardPlanResult planned = RewardResolver.Plan(definition, tier, session, _catalog);

            Assert.IsTrue(planned.Succeeded, package + " did not plan: " + planned.Rejection);
            return RewardResolver.Apply(planned.Plan, session, _catalog);
        }

        private SessionState Session(int tokens)
        {
            Assert.IsTrue(_catalog.TryGetStarterArchetype(new StarterArchetypeID("WB-ARCH-001"), out StarterArchetypeDefinition archetype));

            SessionState session = SessionState.Create(_catalog, archetype, "WB-SYS-001", "session-1", "seed-1");
            if (tokens > 0)
            {
                session.Economy.Tokens.Credit(new TokenAmount(tokens), TokenBasis.RewardTokens, "prior Process");
            }

            return session;
        }

        private static InstanceID OrdinaryHost(SessionState session)
        {
            IReadOnlyList<RepositoryEntry> entries = session.Repository.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Item.Kind == RepositoryItemKind.Instruction)
                    return entries[i].Item.InstanceID;
            }

            Assert.Fail("the starter archetype seeds no Instruction");
            return default;
        }

        private static List<string> Contents(ShopState shop)
        {
            List<string> contents = new(shop.Slots.Count);
            for (int i = 0; i < shop.Slots.Count; i++)
            {
                contents.Add(shop.Slots[i].Offer.ContentID);
            }

            return contents;
        }

        private static bool Holds(IReadOnlyList<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                    return true;
            }

            return false;
        }

        private static ContentCatalog Load()
        {
            string root = Path.Combine(UnityEngine.Application.dataPath, "StreamingAssets", "Catalog");
            CatalogDirectorySource source = new(root);
            CatalogLoader loader = new(new CatalogJsonReader(), new CatalogValidator(), new CatalogFreezer(), source);
            return loader.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
    }
}
