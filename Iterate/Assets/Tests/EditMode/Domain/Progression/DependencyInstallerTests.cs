using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests Dependency installation as one atomic destroy-refund-purchase transaction: everything is
    /// validated against the state after the releases and refunds, and nothing commits until all of it
    /// passes. The refund may fund the purchase that destroys its Dependency, which is the embedded
    /// case UX-MOD-029 permits, so RAM and Tokens are both checked against post-destruction totals.
    /// </summary>
    public sealed class DependencyInstallerTests
    {
        private const string OneRAM = EconomyFixtures.OneRAMDependency;
        private const string TwoRAM = EconomyFixtures.TwoRAMDependency;

        [Test]
        public void RefundFor_HalvesThePricePaidAndFloorsOnce()
        {
            DependencyInstaller installer = new(EconomyFixtures.SessionWithTokens(0));

            Assert.AreEqual(2, installer.RefundFor(4));
            Assert.AreEqual(2, installer.RefundFor(5));
            Assert.AreEqual(3, installer.RefundFor(6));
            Assert.AreEqual(3, installer.RefundFor(7));
            Assert.AreEqual(4, installer.RefundFor(8));
            Assert.AreEqual(0, installer.RefundFor(0));
        }

        [Test]
        public void Install_ReservesRAMAndDebitsThePrice()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(4);
            DependencyInstaller installer = new(session);

            DependencyInstallationResult result = installer.Install(Request(OneRAM, 1, 4));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
            Assert.AreEqual(1, session.Economy.Dependencies.Usage);
            Assert.AreEqual(2, session.Economy.Dependencies.AllInstalled.Count);
        }

        [Test]
        public void Install_RecordsTokenThenInstallationThenReservation()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(4);
            DependencyInstaller installer = new(session);

            DependencyInstallationResult result = installer.Install(Request(OneRAM, 1, 4));

            Assert.AreEqual(TransactionKind.DependencyInstallation, result.Record.Kind);
            Assert.AreEqual(3, result.Record.Consequences.Count);
            Assert.IsInstanceOf<TokenConsequence>(result.Record.Consequences[0]);
            Assert.IsInstanceOf<DependencyInstalled>(result.Record.Consequences[1]);
            Assert.IsInstanceOf<RAMReservation>(result.Record.Consequences[2]);
        }

        [Test]
        public void Install_WithoutFreeRAM_RejectsInsufficientRAMAndChangesNothing()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(20);
            DependencyInstaller installer = new(session);
            installer.Install(Request(TwoRAM, 2, 8));
            installer.Install(Request(TwoRAM, 2, 8, "WB-DEP-203"));
            TokenAmount before = session.Economy.Tokens.Balance;
            int records = session.Economy.Records.Count;

            DependencyInstallationResult result = installer.Install(Request(OneRAM, 1, 4));

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(DependencyRejection.InsufficientRAM, result.Rejection);
            Assert.AreEqual(before, session.Economy.Tokens.Balance);
            Assert.AreEqual(4, session.Economy.Dependencies.Usage);
            Assert.AreEqual(records, session.Economy.Records.Count);
        }

        [Test]
        public void Install_WithoutEnoughTokens_RejectsAndChangesNothing()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(3);
            DependencyInstaller installer = new(session);

            DependencyInstallationResult result = installer.Install(Request(OneRAM, 1, 4));

            Assert.AreEqual(DependencyRejection.InsufficientTokens, result.Rejection);
            Assert.AreEqual(new TokenAmount(3), session.Economy.Tokens.Balance);
            Assert.AreEqual(0, session.Economy.Dependencies.Usage);
            Assert.AreEqual(0, session.Economy.Records.Count);
        }

        [Test]
        public void Install_DestroyingFirst_FreesTheRAMTheInstallationNeeds()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(8);
            DependencyInstaller installer = new(session);
            DependencyInstallationResult installed = installer.Install(Request(TwoRAM, 2, 8));
            InstanceID existing = installed.Installed.Instance.InstanceID;
            session.Economy.Tokens.Credit(new TokenAmount(4), TokenBasis.RewardTokens, "top-up");

            DependencyInstallationResult result = installer.Install(
                Request("WB-DEP-204", 4, 8, null, new[] { existing }));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(4, session.Economy.Dependencies.Usage);
            Assert.IsFalse(session.Economy.Dependencies.TryGet(existing, out InstalledDependency _));
        }

        [Test]
        public void Install_DestroyingFirst_CreditsTheRefundBeforeDebitingThePrice()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(8);
            DependencyInstaller installer = new(session);
            DependencyInstallationResult installed = installer.Install(Request(TwoRAM, 2, 8));
            InstanceID existing = installed.Installed.Instance.InstanceID;
            int entriesBefore = session.Economy.Tokens.Entries.Count;

            DependencyInstallationResult result = installer.Install(
                Request(OneRAM, 1, 4, "WB-DEP-205", new[] { existing }));

            Assert.IsTrue(result.Succeeded, "the refund funds the embedded purchase");
            Assert.IsFalse(session.Economy.Tokens.Entries[entriesBefore].IsDebit);
            Assert.AreEqual(TokenBasis.DestructionRefund, session.Economy.Tokens.Entries[entriesBefore].Basis);
            Assert.AreEqual(new TokenAmount(4), session.Economy.Tokens.Entries[entriesBefore].Amount);
            Assert.IsTrue(session.Economy.Tokens.Entries[entriesBefore + 1].IsDebit);
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
        }

        [Test]
        public void Install_DestroyingFirst_OrdersConsequencesDestroyReleaseRefundThenPurchase()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(8);
            DependencyInstaller installer = new(session);
            InstanceID existing = installer.Install(Request(TwoRAM, 2, 8)).Installed.Instance.InstanceID;

            DependencyInstallationResult result = installer.Install(
                Request(OneRAM, 1, 4, "WB-DEP-205", new[] { existing }));

            IReadOnlyList<TransactionConsequence> consequences = result.Record.Consequences;
            Assert.AreEqual(6, consequences.Count);
            Assert.IsInstanceOf<DependencyDestroyed>(consequences[0]);
            Assert.IsInstanceOf<RAMRelease>(consequences[1]);
            Assert.IsInstanceOf<TokenConsequence>(consequences[2]);
            Assert.IsInstanceOf<TokenConsequence>(consequences[3]);
            Assert.IsInstanceOf<DependencyInstalled>(consequences[4]);
            Assert.IsInstanceOf<RAMReservation>(consequences[5]);
        }

        [Test]
        public void Install_DestroyingTheStarter_RejectsStarterPermanent()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(8);
            DependencyInstaller installer = new(session);

            DependencyInstallationResult result = installer.Install(
                Request(OneRAM, 1, 4, null, new[] { session.StarterDependency.InstanceID }));

            Assert.AreEqual(DependencyRejection.StarterPermanent, result.Rejection);
            Assert.AreEqual(new TokenAmount(8), session.Economy.Tokens.Balance);
        }

        [Test]
        public void Install_DestroyingAnUnknownInstance_RejectsUnknownInstance()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(8);
            DependencyInstaller installer = new(session);

            DependencyInstallationResult result = installer.Install(
                Request(OneRAM, 1, 4, null, new[] { new InstanceID(9999) }));

            Assert.AreEqual(DependencyRejection.UnknownInstance, result.Rejection);
        }

        [Test]
        public void Install_DestroyingOneTargetTwice_RejectsDuplicateDestroyTarget()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(12);
            DependencyInstaller installer = new(session);
            InstanceID existing = installer.Install(Request(TwoRAM, 2, 8)).Installed.Instance.InstanceID;
            TokenAmount before = session.Economy.Tokens.Balance;

            DependencyInstallationResult result = installer.Install(
                Request(OneRAM, 1, 4, "WB-DEP-205", new[] { existing, existing }));

            Assert.AreEqual(DependencyRejection.DuplicateDestroyTarget, result.Rejection);
            Assert.AreEqual(before, session.Economy.Tokens.Balance);
            Assert.IsTrue(session.Economy.Dependencies.TryGet(existing, out InstalledDependency _));
        }

        [Test]
        public void Install_ARejectedComposite_LeavesRackLedgerAndRecordsUnchanged()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(8);
            DependencyInstaller installer = new(session);
            InstanceID existing = installer.Install(Request(TwoRAM, 2, 8)).Installed.Instance.InstanceID;
            int usage = session.Economy.Dependencies.Usage;
            int installedCount = session.Economy.Dependencies.AllInstalled.Count;
            TokenAmount balance = session.Economy.Tokens.Balance;
            int ledgerEntries = session.Economy.Tokens.Entries.Count;
            int records = session.Economy.Records.Count;

            DependencyInstallationResult result = installer.Install(
                Request("WB-DEP-206", 1, 99, null, new[] { existing }));

            Assert.AreEqual(DependencyRejection.InsufficientTokens, result.Rejection);
            Assert.AreEqual(usage, session.Economy.Dependencies.Usage);
            Assert.AreEqual(installedCount, session.Economy.Dependencies.AllInstalled.Count);
            Assert.AreEqual(balance, session.Economy.Tokens.Balance);
            Assert.AreEqual(ledgerEntries, session.Economy.Tokens.Entries.Count);
            Assert.AreEqual(records, session.Economy.Records.Count);
        }

        [Test]
        public void Install_AtPriceZeroAsAReward_CostsNothingAndStillReservesRAM()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(0);
            DependencyInstaller installer = new(session);

            DependencyInstallationResult result = installer.Install(
                new DependencyInstallationRequest(
                    EconomyFixtures.Dependency(OneRAM, 1),
                    0,
                    DependencyOrigin.Reward,
                    new InstanceID[0],
                    "WB-RWD-005:2"));

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, session.Economy.Dependencies.Usage);
            Assert.AreEqual(DependencyOrigin.Reward, result.Installed.Origin);
            Assert.AreEqual(0, result.Installed.PricePaid);
        }

        [Test]
        public void Destroy_ARewardedDependency_RefundsZeroAndStillReleasesRAM()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(0);
            DependencyInstaller installer = new(session);
            InstanceID rewarded = installer.Install(
                new DependencyInstallationRequest(
                    EconomyFixtures.Dependency(OneRAM, 1),
                    0,
                    DependencyOrigin.Reward,
                    new InstanceID[0],
                    "WB-RWD-005:2")).Installed.Instance.InstanceID;

            DependencyDestructionResult result = installer.Destroy(rewarded);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(0, result.Refund);
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
            Assert.AreEqual(0, session.Economy.Dependencies.Usage);
        }

        [Test]
        public void Destroy_CreditsTheRefundAndReleasesRAMInOneRecord()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(8);
            DependencyInstaller installer = new(session);
            InstanceID existing = installer.Install(Request(TwoRAM, 2, 8)).Installed.Instance.InstanceID;

            DependencyDestructionResult result = installer.Destroy(existing);

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(4, result.Refund);
            Assert.AreEqual(new TokenAmount(4), session.Economy.Tokens.Balance);
            Assert.AreEqual(0, session.Economy.Dependencies.Usage);
            Assert.AreEqual(TransactionKind.DependencyDestruction, result.Record.Kind);
            Assert.AreEqual(3, result.Record.Consequences.Count);
            Assert.IsInstanceOf<DependencyDestroyed>(result.Record.Consequences[0]);
            Assert.IsInstanceOf<RAMRelease>(result.Record.Consequences[1]);
            Assert.IsInstanceOf<TokenConsequence>(result.Record.Consequences[2]);
        }

        [Test]
        public void Destroy_TheStarter_RejectsStarterPermanent()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(0);
            DependencyInstaller installer = new(session);

            DependencyDestructionResult result = installer.Destroy(session.StarterDependency.InstanceID);

            Assert.AreEqual(DependencyRejection.StarterPermanent, result.Rejection);
            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
            Assert.AreEqual(0, session.Economy.Records.Count);
        }

        [Test]
        public void Destroy_AnUnknownInstance_RejectsUnknownInstance()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(0);
            DependencyInstaller installer = new(session);

            DependencyDestructionResult result = installer.Destroy(new InstanceID(9999));

            Assert.AreEqual(DependencyRejection.UnknownInstance, result.Rejection);
        }

        [Test]
        public void Preview_MatchesWhatInstallThenCommits()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(8);
            DependencyInstaller installer = new(session);
            InstanceID existing = installer.Install(Request(TwoRAM, 2, 8)).Installed.Instance.InstanceID;
            DependencyInstallationRequest request = Request(OneRAM, 1, 4, "WB-DEP-205", new[] { existing });

            DependencyInstallationPreview preview = installer.Preview(request);
            installer.Install(request);

            Assert.AreEqual(DependencyRejection.None, preview.Rejection);
            Assert.AreEqual(2, preview.RAMFreed);
            Assert.AreEqual(1, preview.RAMAfter);
            Assert.AreEqual(4, preview.Refunds);
            Assert.AreEqual(4, preview.Price);
            Assert.AreEqual(session.Economy.Tokens.Balance, preview.TokensAfter);
            Assert.AreEqual(session.Economy.Dependencies.Usage, preview.RAMAfter);
        }

        [Test]
        public void PreviewDestroy_MatchesWhatDestroyThenCommits()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(8);
            DependencyInstaller installer = new(session);
            InstanceID existing = installer.Install(Request(TwoRAM, 2, 8)).Installed.Instance.InstanceID;

            DependencyDestructionPreview preview = installer.PreviewDestroy(existing);
            DependencyDestructionResult result = installer.Destroy(existing);

            Assert.AreEqual(DependencyRejection.None, preview.Rejection);
            Assert.AreEqual(2, preview.RAMReleased);
            Assert.AreEqual(8, preview.PricePaid);
            Assert.AreEqual(4, preview.Refund);
            Assert.AreEqual(result.Refund, preview.Refund);
            Assert.AreEqual(session.Economy.Tokens.Balance, preview.TokensAfter);
        }

        [Test]
        public void Preview_OfARejectedRequest_CommitsNothing()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(0);
            DependencyInstaller installer = new(session);

            DependencyInstallationPreview preview = installer.Preview(Request(OneRAM, 1, 4));

            Assert.AreEqual(DependencyRejection.InsufficientTokens, preview.Rejection);
            Assert.AreEqual(0, session.Economy.Records.Count);
            Assert.AreEqual(0, session.Economy.Dependencies.Usage);
        }

        [Test]
        public void Install_ANonDependencyCategory_RejectsNotADependency()
        {
            SessionState session = EconomyFixtures.SessionWithTokens(8);
            DependencyInstaller installer = new(session);
            DependencyDefinition miscategorised = new(
                new DependencyID("WB-DEP-207"),
                "WB-DEP-207",
                "WB-DEP-207",
                ContentCategory.Utility,
                Rarity.Common,
                new[] { "Dependency" },
                1,
                new EffectDefinition[0]);

            DependencyInstallationResult result = installer.Install(
                new DependencyInstallationRequest(
                    miscategorised,
                    4,
                    DependencyOrigin.Purchase,
                    new InstanceID[0],
                    "OFF-200"));

            Assert.AreEqual(DependencyRejection.NotADependency, result.Rejection);
            Assert.AreEqual(new TokenAmount(8), session.Economy.Tokens.Balance);
            Assert.AreEqual(0, session.Economy.Dependencies.Usage);
        }

        private static DependencyInstallationRequest Request(
            string id,
            int ram,
            int price,
            string distinctID = null,
            IReadOnlyList<InstanceID> destroyFirst = null
        )
        {
            return new DependencyInstallationRequest(
                EconomyFixtures.Dependency(distinctID ?? id, ram),
                price,
                DependencyOrigin.Purchase,
                destroyFirst ?? new InstanceID[0],
                "OFF-200");
        }
    }
}
