using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests the shop: numbered slots materialised from a definition, free pinning, escalating rerolls
    /// that never show content twice and reproduce under one seed, purchases routed by category, and a
    /// close that ends everything. Rerolls are one Determinism decision each, so the suite asserts both
    /// what a reroll costs and that two Sessions on one seed reroll identically.
    /// </summary>
    public sealed class ShopStateTests
    {
        private const string ShopID = "WB-SHOP-101";
        private const string PoolID = "WB-POOL-101";
        private const string ValuePlusTwo = ProgressionFixtures.ValuePlusTwo;
        private const string ValuePlusThree = ProgressionFixtures.ValuePlusThree;
        private const string Utility = EconomyFixtures.StartingBytesUtility;
        private const string Target = "WB-PROC-004";

        [Test]
        public void Open_MaterialisesTheFixedOffersAsNumberedSlots()
        {
            ShopState shop = OpenShop(out SessionState _, 0);

            Assert.AreEqual(3, shop.Slots.Count);
            Assert.AreEqual(1, shop.Slots[0].Number);
            Assert.AreEqual(2, shop.Slots[1].Number);
            Assert.AreEqual(3, shop.Slots[2].Number);
            Assert.AreEqual(ValuePlusTwo, shop.Slots[0].Offer.ContentID);
            Assert.AreEqual(4, shop.Slots[0].Offer.Price);
            Assert.AreEqual(ShopSlotState.Available, shop.Slots[0].State);
            Assert.IsTrue(shop.IsOpen);
        }

        [Test]
        public void Open_SeedsTheSeenSetWithTheFixedOffers()
        {
            ShopState shop = OpenShop(out SessionState _, 0);

            Assert.AreEqual(3, shop.SeenContent.Count);
            Assert.IsTrue(Holds(shop.SeenContent, ValuePlusTwo));
            Assert.IsTrue(Holds(shop.SeenContent, Utility));
        }

        [Test]
        public void Open_WithContentTheCatalogLacks_RejectsContentMissing()
        {
            ContentCatalog catalog = EconomyFixtures.ShopCatalog(
                new[]
                {
                    EconomyFixtures.Shop(
                        ShopID, 1, false, false, false, false,
                        new[] { EconomyFixtures.Offer("OFF-900", "WB-INS-999", 4) },
                        null)
                },
                new PoolDefinition[0]);
            SessionState session = EconomyFixtures.SessionOver(catalog, 0);
            catalog.TryGetShop(new ShopID(ShopID), out ShopDefinition definition);

            ShopOpenResult result = ShopState.Open(definition, catalog, session, new ProcessID(Target));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ShopRejection.ContentMissing, result.Rejection);
        }

        [Test]
        public void Open_WithAPoolTheCatalogLacks_RejectsPoolMissing()
        {
            ContentCatalog catalog = EconomyFixtures.ShopCatalog(
                new[]
                {
                    EconomyFixtures.Shop(
                        ShopID, 1, true, false, false, false,
                        new[] { EconomyFixtures.Offer("OFF-900", ValuePlusTwo, 4) },
                        new PoolID("WB-POOL-999"))
                },
                new PoolDefinition[0]);
            SessionState session = EconomyFixtures.SessionOver(catalog, 0);
            catalog.TryGetShop(new ShopID(ShopID), out ShopDefinition definition);

            ShopOpenResult result = ShopState.Open(definition, catalog, session, new ProcessID(Target));

            Assert.AreEqual(ShopRejection.PoolMissing, result.Rejection);
        }

        [Test]
        public void Pin_IsFreeAndRecorded()
        {
            ShopState shop = OpenShop(out SessionState session, 5);

            Assert.IsTrue(shop.Pin(1).Succeeded);

            Assert.IsTrue(shop.Slots[0].Pinned);
            Assert.AreEqual(new TokenAmount(5), session.Economy.Tokens.Balance);
            Assert.AreEqual(1, shop.Pins.Count);
            Assert.IsTrue(shop.Pins[0].Pinned);
        }

        [Test]
        public void Unpin_ClearsThePinAndIsRecorded()
        {
            ShopState shop = OpenShop(out SessionState _, 5);
            shop.Pin(1);

            Assert.IsTrue(shop.Unpin(1).Succeeded);

            Assert.IsFalse(shop.Slots[0].Pinned);
            Assert.AreEqual(2, shop.Pins.Count);
            Assert.IsFalse(shop.Pins[1].Pinned);
        }

        [Test]
        public void Pin_AnUnknownSlot_RejectsUnknownSlot()
        {
            ShopState shop = OpenShop(out SessionState _, 5);

            Assert.AreEqual(ShopRejection.UnknownSlot, shop.Pin(9).Rejection);
        }

        [Test]
        public void Pin_WhenPinningIsDisabled_RejectsPinningDisabled()
        {
            ShopState shop = OpenShop(out SessionState _, 5, pinning: false);

            Assert.AreEqual(ShopRejection.PinningDisabled, shop.Pin(1).Rejection);
        }

        [Test]
        public void Reroll_WhenDisabled_RejectsRerollsDisabled()
        {
            ShopState shop = OpenShop(out SessionState _, 5, rerolls: false);

            Assert.AreEqual(ShopRejection.RerollsDisabled, shop.Reroll().Rejection);
            Assert.IsNull(shop.NextRerollCost);
        }

        [Test]
        public void Reroll_CostsOneThenTwoThenThree()
        {
            ShopState shop = OpenShop(out SessionState session, 6);

            Assert.AreEqual(1, shop.NextRerollCost);
            shop.Reroll();
            Assert.AreEqual(new TokenAmount(5), session.Economy.Tokens.Balance);
            Assert.AreEqual(2, shop.NextRerollCost);
            shop.Reroll();
            Assert.AreEqual(new TokenAmount(3), session.Economy.Tokens.Balance);
            Assert.AreEqual(3, shop.NextRerollCost);
            shop.Reroll();
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
            Assert.IsNull(shop.NextRerollCost);
        }

        [Test]
        public void Reroll_AFourthTime_RejectsRerollLimitReached()
        {
            ShopState shop = OpenShop(out SessionState session, 12);
            shop.Reroll();
            shop.Reroll();
            shop.Reroll();
            TokenAmount before = session.Economy.Tokens.Balance;

            RerollResult fourth = shop.Reroll();

            Assert.AreEqual(ShopRejection.RerollLimitReached, fourth.Rejection);
            Assert.AreEqual(before, session.Economy.Tokens.Balance);
            Assert.AreEqual(3, shop.RerollsUsed);
            Assert.AreEqual(3, ShopState.MaximumRerolls);
        }

        [Test]
        public void Reroll_WithoutTokens_RejectsInsufficientTokensAndReplacesNothing()
        {
            ShopState shop = OpenShop(out SessionState session, 0);
            string before = shop.Slots[0].Offer.ContentID;

            RerollResult result = shop.Reroll();

            Assert.AreEqual(ShopRejection.InsufficientTokens, result.Rejection);
            Assert.AreEqual(before, shop.Slots[0].Offer.ContentID);
            Assert.AreEqual(0, shop.RerollsUsed);
            Assert.AreEqual(0, session.Economy.Records.Count);
        }

        [Test]
        public void Reroll_ReplacesEveryUnpinnedSlotAndKeepsThePinnedOne()
        {
            ShopState shop = OpenShop(out SessionState _, 6);
            shop.Pin(1);
            string pinned = shop.Slots[0].Offer.ContentID;

            RerollResult result = shop.Reroll();

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(pinned, shop.Slots[0].Offer.ContentID);
            Assert.AreEqual(2, result.ReplacedSlots.Count);
            Assert.AreEqual(2, result.ReplacedSlots[0]);
            Assert.AreEqual(3, result.ReplacedSlots[1]);
        }

        [Test]
        public void Reroll_NeverShowsContentAlreadySeen()
        {
            ShopState shop = OpenShop(out SessionState _, 6);
            List<string> seenBefore = EconomyFixtures.Copy(shop.SeenContent);

            shop.Reroll();

            for (int i = 0; i < shop.Slots.Count; i++)
            {
                if (shop.Slots[i].Pinned)
                    continue;

                Assert.IsFalse(
                    Holds(seenBefore, shop.Slots[i].Offer.ContentID),
                    shop.Slots[i].Offer.ContentID + " was already shown in this shop");
            }
        }

        [Test]
        public void Reroll_WithTooLittleUnseenStock_RejectsInsufficientUnseenStockWithNoDebit()
        {
            ContentCatalog catalog = Catalog(poolContent: new[] { "WB-INS-003" });
            SessionState session = EconomyFixtures.SessionOver(catalog, 6);
            catalog.TryGetShop(new ShopID(ShopID), out ShopDefinition definition);
            ShopState shop = ShopState.Open(definition, catalog, session, new ProcessID(Target)).Shop;

            RerollResult result = shop.Reroll();

            Assert.AreEqual(ShopRejection.InsufficientUnseenStock, result.Rejection);
            Assert.AreEqual(new TokenAmount(6), session.Economy.Tokens.Balance);
        }

        [Test]
        public void Reroll_RecordsOneDeterminismDecisionAndOneTransaction()
        {
            ShopState shop = OpenShop(out SessionState session, 6);

            RerollResult result = shop.Reroll();

            Assert.IsNotNull(result.Decision);
            Assert.AreEqual(TransactionKind.Reroll, result.Record.Kind);
            Assert.AreEqual(1, shop.Transactions.Count);
            Assert.AreEqual(1, session.Economy.Records.Count);
        }

        [Test]
        public void Reroll_UnderTheSameSeed_ProducesTheSameOffers()
        {
            ShopState first = OpenShop(out SessionState _, 6);
            ShopState second = OpenShop(out SessionState _, 6);

            first.Reroll();
            second.Reroll();

            for (int i = 0; i < first.Slots.Count; i++)
            {
                Assert.AreEqual(first.Slots[i].Offer.ContentID, second.Slots[i].Offer.ContentID);
            }
        }

        [Test]
        public void Purchase_AnItemOffer_DebitsAndAcquiresIt()
        {
            ShopState shop = OpenShop(out SessionState session, 4);
            int before = session.Repository.Entries.Count;

            PurchaseResult result = shop.Purchase(1);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
            Assert.AreEqual(before + 1, session.Repository.Entries.Count);
            Assert.AreEqual(AcquisitionOrigin.Purchase, result.Acquired.Origin);
            Assert.AreEqual(ShopSlotState.Sold, shop.Slots[0].State);
        }

        [Test]
        public void Purchase_ASoldSlot_RejectsSlotNotAvailable()
        {
            ShopState shop = OpenShop(out SessionState session, 8);
            shop.Purchase(1);

            PurchaseResult second = shop.Purchase(1);

            Assert.AreEqual(ShopRejection.SlotNotAvailable, second.Rejection);
            Assert.AreEqual(new TokenAmount(4), session.Economy.Tokens.Balance);
        }

        [Test]
        public void Purchase_WithoutTokens_RejectsInsufficientTokensAndLeavesTheSlotAvailable()
        {
            ShopState shop = OpenShop(out SessionState session, 3);

            PurchaseResult result = shop.Purchase(1);

            Assert.AreEqual(ShopRejection.InsufficientTokens, result.Rejection);
            Assert.AreEqual(ShopSlotState.Available, shop.Slots[0].State);
            Assert.AreEqual(new TokenAmount(3), session.Economy.Tokens.Balance);
        }

        [Test]
        public void Purchase_AUtilityOffer_CommitsItForTheTargetProcess()
        {
            ShopState shop = OpenShop(out SessionState session, 3);

            PurchaseResult result = shop.Purchase(3);

            Assert.IsTrue(result.Succeeded);
            Assert.IsNotNull(result.Committed);
            Assert.AreEqual(new ProcessID(Target), result.Committed.TargetProcess);
            Assert.AreEqual(1, session.Economy.Utilities.Count);
            Assert.AreEqual(1, session.Economy.SetupEffectsFor(new ProcessID(Target)).Count);
        }

        [Test]
        public void Purchase_APatchOffer_RejectsNotARoutinePurchase()
        {
            ContentCatalog catalog = Catalog(patchSlot: true);
            SessionState session = EconomyFixtures.SessionOver(catalog, 8);
            catalog.TryGetShop(new ShopID(ShopID), out ShopDefinition definition);
            ShopState shop = ShopState.Open(definition, catalog, session, new ProcessID(Target)).Shop;

            PurchaseResult result = shop.Purchase(2);

            Assert.AreEqual(ShopRejection.NotARoutinePurchase, result.Rejection);
            Assert.AreEqual(new TokenAmount(8), session.Economy.Tokens.Balance);
        }

        [Test]
        public void PurchasePatch_OntoAnEmptySocket_SucceedsWithServicesDisabled()
        {
            ContentCatalog catalog = Catalog(patchSlot: true, services: false);
            SessionState session = EconomyFixtures.SessionOver(catalog, 8);
            catalog.TryGetShop(new ShopID(ShopID), out ShopDefinition definition);
            ShopState shop = ShopState.Open(definition, catalog, session, new ProcessID(Target)).Shop;
            InstanceID host = session.Repository.Entries[0].Item.InstanceID;

            PatchAttachmentResult result = shop.PurchasePatch(2, host, 1);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(ShopSlotState.Sold, shop.Slots[1].State);
        }

        [Test]
        public void PurchasePatch_IntoAnOccupiedSocket_RequiresServices()
        {
            ContentCatalog catalog = Catalog(patchSlot: true, services: false);
            SessionState session = EconomyFixtures.SessionOver(catalog, 20);
            catalog.TryGetShop(new ShopID(ShopID), out ShopDefinition definition);
            ShopState shop = ShopState.Open(definition, catalog, session, new ProcessID(Target)).Shop;
            InstanceID host = session.Repository.Entries[0].Item.InstanceID;
            new PatchInstaller(session).Attach(new PatchAttachmentRequest(
                EconomyFixtures.PatchWithRule("WB-PAT-001", "FIXED_NUMBER_ADDITION_HOSTS"),
                host,
                1,
                4,
                null,
                "setup"));

            PatchAttachmentResult result = shop.PurchasePatch(2, host, 1);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(PatchAttachmentRejection.ServicesNotOffered, result.Rejection);
            Assert.AreEqual(ShopSlotState.Available, shop.Slots[1].State);
        }

        [Test]
        public void PurchaseDependency_InADependenciesDisabledShop_Rejects()
        {
            ShopState shop = OpenShop(out SessionState session, 8);

            DependencyInstallationResult result = shop.PurchaseDependency(1, new InstanceID[0]);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(DependencyRejection.DependenciesNotOffered, result.Rejection);
            Assert.AreEqual(new TokenAmount(8), session.Economy.Tokens.Balance);
        }

        [Test]
        public void DeleteItem_InAServicesDisabledShop_Rejects()
        {
            ShopState shop = OpenShop(out SessionState session, 8, services: false);
            InstanceID item = session.Repository.Entries[0].Item.InstanceID;

            ServiceResult result = shop.DeleteItem(item);

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(ServiceRejection.ServicesNotOffered, result.Rejection);
            Assert.AreEqual(new TokenAmount(8), session.Economy.Tokens.Balance);
        }

        [Test]
        public void DuplicateItem_InAServicesEnabledShop_Succeeds()
        {
            ShopState shop = OpenShop(out SessionState session, 8, services: true);
            InstanceID item = session.Repository.Entries[0].Item.InstanceID;

            ServiceResult result = shop.DuplicateItem(item);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(new TokenAmount(1), session.Economy.Tokens.Balance);
        }

        [Test]
        public void Close_ClearsPinsAndRejectsEveryLaterAction()
        {
            ShopState shop = OpenShop(out SessionState _, 8);
            shop.Pin(1);

            shop.Close();

            Assert.IsFalse(shop.IsOpen);
            Assert.IsFalse(shop.Slots[0].Pinned);
            Assert.AreEqual(ShopRejection.ShopClosed, shop.Pin(1).Rejection);
            Assert.AreEqual(ShopRejection.ShopClosed, shop.Reroll().Rejection);
            Assert.AreEqual(ShopRejection.ShopClosed, shop.Purchase(1).Rejection);
        }

        [Test]
        public void PreviewReroll_MatchesWhatRerollThenCosts()
        {
            ShopState shop = OpenShop(out SessionState session, 6);

            RerollPreview preview = shop.PreviewReroll();
            shop.Reroll();

            Assert.AreEqual(ShopRejection.None, preview.Rejection);
            Assert.AreEqual(1, preview.Cost);
            Assert.AreEqual(3, preview.OpenSlots);
            Assert.AreEqual(session.Economy.Tokens.Balance, preview.TokensAfter);
        }

        [Test]
        public void PreviewPurchase_MatchesWhatPurchaseThenCosts()
        {
            ShopState shop = OpenShop(out SessionState session, 4);

            PurchasePreview preview = shop.PreviewPurchase(1);
            shop.Purchase(1);

            Assert.AreEqual(ShopRejection.None, preview.Rejection);
            Assert.AreEqual(4, preview.Price);
            Assert.AreEqual(ContentCategory.Instruction, preview.Category);
            Assert.AreEqual(session.Economy.Tokens.Balance, preview.TokensAfter);
        }

        [Test]
        public void Reroll_TreatsASoldSlotAsOpen()
        {
            ShopState shop = OpenShop(out SessionState _, 10);
            shop.Purchase(1);

            RerollResult result = shop.Reroll();

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(3, result.ReplacedSlots.Count);
            Assert.AreEqual(ShopSlotState.Available, shop.Slots[0].State);
        }

        private static ShopState OpenShop(
            out SessionState session,
            int tokens,
            bool rerolls = true,
            bool pinning = true,
            bool services = true
        )
        {
            ContentCatalog catalog = Catalog(rerolls: rerolls, pinning: pinning, services: services);
            session = EconomyFixtures.SessionOver(catalog, tokens);
            catalog.TryGetShop(new ShopID(ShopID), out ShopDefinition definition);
            return ShopState.Open(definition, catalog, session, new ProcessID(Target)).Shop;
        }

        private static ContentCatalog Catalog(
            bool rerolls = true,
            bool pinning = true,
            bool services = true,
            bool patchSlot = false,
            string[] poolContent = null
        )
        {
            List<ShopOffer> offers = new List<ShopOffer>
            {
                EconomyFixtures.Offer("OFF-901", ValuePlusTwo, 4),
                patchSlot
                    ? EconomyFixtures.Offer("OFF-902", "WB-PAT-001", 4)
                    : EconomyFixtures.Offer("OFF-902", ValuePlusThree, 4),
                EconomyFixtures.Offer("OFF-903", Utility, 3)
            };

            // Three rerolls of a three-slot shop need nine unseen members: a reroll draws one per open
            // slot and never repeats content, so a shorter pool exhausts before the third reroll and
            // rejects InsufficientUnseenStock rather than reaching the reroll limit.
            string[] members = poolContent ?? new[]
            {
                "WB-INS-012", "WB-STR-001", "WB-STR-002", "WB-DIR-001", "WB-DEP-001",
                "WB-DEP-002", "WB-DEP-201", "WB-DEP-202", "WB-PAT-001"
            };
            PoolDefinition pool = EconomyFixtures.Pool(PoolID, 4, members);

            return EconomyFixtures.ShopCatalog(
                new[]
                {
                    EconomyFixtures.Shop(
                        ShopID, 3, rerolls, pinning, false, services, offers, new PoolID(PoolID))
                },
                new[] { pool });
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
    }
}
