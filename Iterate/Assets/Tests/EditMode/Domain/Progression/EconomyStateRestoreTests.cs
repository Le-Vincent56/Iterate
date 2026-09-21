using System;
using System.Collections.Generic;
using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests <see cref="EconomyState.Restore"/> and the <see cref="EconomyState.TransactionCounters"/>
    /// it round-trips through: a restored economy reports the same balance, RAM, commitments and grants,
    /// and continues minting per-kind transaction identities above the counts it was given rather than
    /// reissuing one. Transaction records and Token ledger entries are not restored — they describe what
    /// happened, not what the Session holds.
    /// </summary>
    public sealed class EconomyStateRestoreTests
    {
        [Test]
        public void TransactionCounters_OfAFreshEconomy_IsEmpty()
        {
            EconomyState economy = FreshEconomy();

            Assert.AreEqual(0, economy.TransactionCounters.Count);
        }

        [Test]
        public void TransactionCounters_ReportOnlyTheKindsUsed()
        {
            EconomyState economy = FreshEconomy();
            economy.NextTransactionIdentity(TransactionKind.Purchase);
            economy.NextTransactionIdentity(TransactionKind.Purchase);
            economy.NextTransactionIdentity(TransactionKind.Reroll);

            IReadOnlyList<TransactionCounter> counters = economy.TransactionCounters;

            Assert.AreEqual(2, counters.Count);
            Assert.AreEqual(new TransactionCounter(TransactionKind.Purchase, 2), counters[0]);
            Assert.AreEqual(new TransactionCounter(TransactionKind.Reroll, 1), counters[1]);
        }

        [Test]
        public void TransactionCounters_OrderByKindRegardlessOfUseOrder()
        {
            // Reward is the last enum member and Purchase the first, so using them in reverse order
            // proves the order is the enum's rather than the dictionary's.
            EconomyState economy = FreshEconomy();
            economy.NextTransactionIdentity(TransactionKind.Reward);
            economy.NextTransactionIdentity(TransactionKind.Purchase);

            IReadOnlyList<TransactionCounter> counters = economy.TransactionCounters;

            Assert.AreEqual(TransactionKind.Purchase, counters[0].Kind);
            Assert.AreEqual(TransactionKind.Reward, counters[1].Kind);
        }

        [Test]
        public void Restore_ReproducesTheTokenBalance()
        {
            EconomyState economy = RestoredEconomy();

            Assert.AreEqual(7, economy.Tokens.Balance.Value);
        }

        [Test]
        public void Restore_LeavesTheTokenLedgerEntriesEmpty()
        {
            EconomyState economy = RestoredEconomy();

            Assert.AreEqual(0, economy.Tokens.Entries.Count, "a ledger entry records a movement, not a balance.");
        }

        [Test]
        public void Restore_ReproducesTheRackAndItsFreeRAM()
        {
            EconomyState economy = RestoredEconomy();

            Assert.AreEqual(5, economy.Dependencies.Capacity);
            Assert.AreEqual(1, economy.Dependencies.Installed.Count);
            Assert.AreEqual(1, economy.Dependencies.Usage, "the restored rack recomputes usage from what it holds.");
            Assert.AreEqual(4, economy.Dependencies.Free);
            Assert.AreEqual(0, economy.Dependencies.Starter.Definition.RAM);
        }

        [Test]
        public void Restore_ReproducesCommitmentsAndGrants()
        {
            EconomyState economy = RestoredEconomy();

            Assert.AreEqual(1, economy.Utilities.Count);
            Assert.AreEqual(EconomyFixtures.Process("WB-PROC-004"), economy.Utilities[0].TargetProcess);
            Assert.AreEqual(1, economy.PatchGrants.Count);
            Assert.IsTrue(economy.TryGetGrant(economy.PatchGrants[0].GrantIdentity, out PatchGrant grant));
            Assert.IsTrue(grant.Cached);
        }

        [Test]
        public void Restore_ContinuesTransactionIdentitiesAboveTheRestoredCounts()
        {
            EconomyState economy = RestoredEconomy();

            Assert.AreEqual("purchase:3", economy.NextTransactionIdentity(TransactionKind.Purchase));
            Assert.AreEqual("reroll:1", economy.NextTransactionIdentity(TransactionKind.Reroll));
        }

        [Test]
        public void Restore_LeavesRecordsEmpty()
        {
            EconomyState economy = RestoredEconomy();

            Assert.AreEqual(0, economy.Records.Count, "restoring is not a transaction.");
        }

        [Test]
        public void Restore_RoundTripsTheTransactionCounters()
        {
            EconomyState economy = RestoredEconomy();

            Assert.AreEqual(1, economy.TransactionCounters.Count);
            Assert.AreEqual(new TransactionCounter(TransactionKind.Purchase, 2), economy.TransactionCounters[0]);
        }

        [Test]
        public void Restore_KeepsTheParameterRegister()
        {
            EconomyState economy = RestoredEconomy();

            Assert.AreEqual(ProgressionFixtures.Parameters().StartingRAM, economy.Parameters.StartingRAM);
        }

        [Test]
        public void Restore_ContinuesAllocatingFromTheGivenIdentitySource()
        {
            InstanceIDSource instanceIDs = InstanceIDSource.Restore(9);
            EconomyState economy = EconomyState.Restore(
                instanceIDs,
                ProgressionFixtures.Parameters(),
                new TokenLedger(new TokenAmount(0)),
                new DependencyRack(Starter(), 5),
                Array.Empty<UtilityCommitment>(),
                Array.Empty<PatchGrant>(),
                Array.Empty<TransactionCounter>()
            );

            PatchGrant granted = economy.Grant(ProgressionFixtures.Patch("WB-PAT-001"), false, "fixture");

            Assert.AreEqual(new InstanceID(10), granted.GrantIdentity);
        }

        /// <summary>
        /// Builds the zero-RAM starter Dependency instance every rack in this suite opens with.
        /// </summary>
        /// <returns>The starter instance.</returns>
        private static DependencyInstance Starter()
        {
            return new DependencyInstance(
                new InstanceID(1),
                ProgressionFixtures.Dependency(ProgressionFixtures.StandardLibrary, 0)
            );
        }

        /// <summary>
        /// Builds an economy as a Session start produces one.
        /// </summary>
        /// <returns>The fresh economy.</returns>
        private static EconomyState FreshEconomy()
        {
            return new EconomyState(new InstanceIDSource(), Starter(), ProgressionFixtures.Parameters());
        }

        /// <summary>
        /// Restores an economy holding 7 Tokens, a capacity of 5 with one 1-RAM Dependency installed,
        /// one Utility commitment, one cached Patch grant and two Purchases already counted.
        /// </summary>
        /// <returns>The restored economy.</returns>
        private static EconomyState RestoredEconomy()
        {
            DependencyRack rack = new(Starter(), 5);
            rack.Install(
                ProgressionFixtures.Dependency(ProgressionFixtures.CleanBuild, 1),
                new InstanceID(2),
                3,
                DependencyOrigin.Purchase
            );

            List<UtilityCommitment> utilities = new()
            {
                new UtilityCommitment(
                    new InstanceID(3),
                    EconomyFixtures.Utility(EconomyFixtures.StartingBytesUtility, "STARTING_BYTES", 1),
                    EconomyFixtures.Process("WB-PROC-004"),
                    "OFF-001"
                )
            };

            List<PatchGrant> grants = new()
            {
                new PatchGrant(new InstanceID(4), ProgressionFixtures.Patch("WB-PAT-001"), true, "fixture")
            };

            return EconomyState.Restore(
                InstanceIDSource.Restore(4),
                ProgressionFixtures.Parameters(),
                new TokenLedger(new TokenAmount(7)),
                rack,
                utilities,
                grants,
                new List<TransactionCounter> { new(TransactionKind.Purchase, 2) }
            );
        }
    }
}
