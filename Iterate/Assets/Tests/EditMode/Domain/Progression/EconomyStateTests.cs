using NUnit.Framework;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests the Session's economy state: the holder that owns the Token ledger and the Dependency rack,
    /// records every committed transaction under a per-kind identity, and carries the two commitments a
    /// purchase can leave behind — a Utility bound to one Process, and a Patch grant waiting for a host.
    /// Reading a commitment never consumes it; only the Process passing, or the attachment landing, does.
    /// </summary>
    public sealed class EconomyStateTests
    {
        private const string TargetProcess = "WB-PROC-004";

        [Test]
        public void NewEconomy_StartsAtZeroTokensWithNoCommitments()
        {
            SessionState session = ProgressionFixtures.Session();

            Assert.AreEqual(new TokenAmount(0), session.Economy.Tokens.Balance);
            Assert.AreEqual(0, session.Economy.Utilities.Count);
            Assert.AreEqual(0, session.Economy.PatchGrants.Count);
            Assert.AreEqual(0, session.Economy.Records.Count);
        }

        [Test]
        public void CommitUtility_RecordsTheCommitmentAgainstItsTargetProcess()
        {
            SessionState session = ProgressionFixtures.Session();

            UtilityCommitmentResult result = session.Economy.CommitUtility(
                EconomyFixtures.Utility(EconomyFixtures.StartingBytesUtility, "STARTING_BYTES", 1),
                EconomyFixtures.Process(TargetProcess),
                "OFF-101");

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, session.Economy.Utilities.Count);
            Assert.AreEqual(EconomyFixtures.Process(TargetProcess), session.Economy.Utilities[0].TargetProcess);
            Assert.AreEqual("OFF-101", session.Economy.Utilities[0].OfferID);
        }

        [Test]
        public void CommitUtility_SameDefinitionAndTargetTwice_IsRejected()
        {
            SessionState session = ProgressionFixtures.Session();
            UtilityDefinition utility = EconomyFixtures.Utility(EconomyFixtures.StartingBytesUtility, "STARTING_BYTES", 1);
            session.Economy.CommitUtility(utility, EconomyFixtures.Process(TargetProcess), "OFF-101");

            UtilityCommitmentResult second = session.Economy.CommitUtility(utility, EconomyFixtures.Process(TargetProcess), "OFF-102");

            Assert.IsFalse(second.Succeeded);
            Assert.AreEqual(1, session.Economy.Utilities.Count);
        }

        [Test]
        public void CommitUtility_SameDefinitionForAnotherProcess_IsAllowed()
        {
            SessionState session = ProgressionFixtures.Session();
            UtilityDefinition utility = EconomyFixtures.Utility(EconomyFixtures.StartingBytesUtility, "STARTING_BYTES", 1);
            session.Economy.CommitUtility(utility, EconomyFixtures.Process(TargetProcess), "OFF-101");

            UtilityCommitmentResult second = session.Economy.CommitUtility(utility, EconomyFixtures.Process("WB-PROC-005"), "OFF-102");

            Assert.IsTrue(second.Succeeded);
            Assert.AreEqual(2, session.Economy.Utilities.Count);
        }

        [Test]
        public void SetupEffectsFor_ReturnsTheCommitmentsEffectsWithoutConsumingThem()
        {
            SessionState session = ProgressionFixtures.Session();
            session.Economy.CommitUtility(
                EconomyFixtures.Utility(EconomyFixtures.StartingBytesUtility, "STARTING_BYTES", 1),
                EconomyFixtures.Process(TargetProcess),
                "OFF-101");

            Assert.AreEqual(1, session.Economy.SetupEffectsFor(EconomyFixtures.Process(TargetProcess)).Count);
            Assert.AreEqual(1, session.Economy.SetupEffectsFor(EconomyFixtures.Process(TargetProcess)).Count);
            Assert.AreEqual(1, session.Economy.Utilities.Count);
        }

        [Test]
        public void SetupEffectsFor_AttributesTheEffectToTheDeclaringUtility()
        {
            SessionState session = ProgressionFixtures.Session();
            session.Economy.CommitUtility(
                EconomyFixtures.Utility(EconomyFixtures.StartingBytesUtility, "STARTING_BYTES", 1),
                EconomyFixtures.Process(TargetProcess),
                "OFF-101");

            ActiveSetupEffect effect = session.Economy.SetupEffectsFor(EconomyFixtures.Process(TargetProcess))[0];

            Assert.AreEqual(EconomyFixtures.StartingBytesUtility, effect.SourceKey);
            Assert.AreEqual("STARTING_BYTES", effect.Modification.Setting);
        }

        [Test]
        public void SetupEffectsFor_AnotherProcess_IsEmpty()
        {
            SessionState session = ProgressionFixtures.Session();
            session.Economy.CommitUtility(
                EconomyFixtures.Utility(EconomyFixtures.StartingBytesUtility, "STARTING_BYTES", 1),
                EconomyFixtures.Process(TargetProcess),
                "OFF-101");

            Assert.AreEqual(0, session.Economy.SetupEffectsFor(EconomyFixtures.Process("WB-PROC-005")).Count);
        }

        [Test]
        public void SetupEffectsFor_AUtilityWithoutEffects_IsEmpty()
        {
            SessionState session = ProgressionFixtures.Session();
            session.Economy.CommitUtility(
                EconomyFixtures.UtilityWithoutEffects("WB-UTL-202"),
                EconomyFixtures.Process(TargetProcess),
                "OFF-101");

            Assert.AreEqual(0, session.Economy.SetupEffectsFor(EconomyFixtures.Process(TargetProcess)).Count);
        }

        [Test]
        public void ExpireUtilitiesFor_RemovesOnlyThatProcessCommitments()
        {
            SessionState session = ProgressionFixtures.Session();
            UtilityDefinition utility = EconomyFixtures.Utility(EconomyFixtures.StartingBytesUtility, "STARTING_BYTES", 1);
            session.Economy.CommitUtility(utility, EconomyFixtures.Process(TargetProcess), "OFF-101");
            session.Economy.CommitUtility(utility, EconomyFixtures.Process("WB-PROC-005"), "OFF-102");

            int expired = session.Economy.ExpireUtilitiesFor(EconomyFixtures.Process(TargetProcess));

            Assert.AreEqual(1, expired);
            Assert.AreEqual(1, session.Economy.Utilities.Count);
            Assert.AreEqual(EconomyFixtures.Process("WB-PROC-005"), session.Economy.Utilities[0].TargetProcess);
        }

        [Test]
        public void ExpireUtilitiesFor_WithNoCommitments_ReturnsZero()
        {
            SessionState session = ProgressionFixtures.Session();

            Assert.AreEqual(0, session.Economy.ExpireUtilitiesFor(EconomyFixtures.Process(TargetProcess)));
        }

        [Test]
        public void Grant_MintsAGrantUnderItsOwnIdentity()
        {
            SessionState session = ProgressionFixtures.Session();

            PatchGrant grant = session.Economy.Grant(ProgressionFixtures.Patch("WB-PAT-001"), false, "WB-RWD-002:1");

            Assert.AreEqual(1, session.Economy.PatchGrants.Count);
            Assert.IsFalse(grant.Cached);
            Assert.AreEqual("WB-RWD-002:1", grant.Basis);
            Assert.IsTrue(session.Economy.TryGetGrant(grant.GrantIdentity, out PatchGrant found));
            Assert.AreEqual(grant, found);
        }

        [Test]
        public void Grant_TwiceOfOneDefinition_AreDistinctGrants()
        {
            SessionState session = ProgressionFixtures.Session();
            PatchDefinition patch = ProgressionFixtures.Patch("WB-PAT-001");

            PatchGrant first = session.Economy.Grant(patch, false, "a");
            PatchGrant second = session.Economy.Grant(patch, false, "b");

            Assert.AreNotEqual(first.GrantIdentity, second.GrantIdentity);
            Assert.AreEqual(2, session.Economy.PatchGrants.Count);
        }

        [Test]
        public void ConsumeGrant_RemovesItByIdentityAndIsNotRepeatable()
        {
            SessionState session = ProgressionFixtures.Session();
            PatchGrant grant = session.Economy.Grant(ProgressionFixtures.Patch("WB-PAT-001"), false, "basis");

            Assert.IsTrue(session.Economy.ConsumeGrant(grant.GrantIdentity));
            Assert.AreEqual(0, session.Economy.PatchGrants.Count);
            Assert.IsFalse(session.Economy.ConsumeGrant(grant.GrantIdentity));
            Assert.IsFalse(session.Economy.TryGetGrant(grant.GrantIdentity, out PatchGrant _));
        }

        [Test]
        public void NextTransactionIdentity_NumbersPerKindFromOne()
        {
            SessionState session = ProgressionFixtures.Session();

            Assert.AreEqual("purchase:1", session.Economy.NextTransactionIdentity(TransactionKind.Purchase));
            Assert.AreEqual("purchase:2", session.Economy.NextTransactionIdentity(TransactionKind.Purchase));
            Assert.AreEqual("reroll:1", session.Economy.NextTransactionIdentity(TransactionKind.Reroll));
            Assert.AreEqual("purchase:3", session.Economy.NextTransactionIdentity(TransactionKind.Purchase));
        }

        [Test]
        public void Append_KeepsRecordsInCommitOrder()
        {
            SessionState session = ProgressionFixtures.Session();
            TransactionRecord first = new(
                TransactionKind.Purchase,
                session.Economy.NextTransactionIdentity(TransactionKind.Purchase),
                new TransactionConsequence[0]);
            TransactionRecord second = new(
                TransactionKind.Reroll,
                session.Economy.NextTransactionIdentity(TransactionKind.Reroll),
                new TransactionConsequence[0]);

            session.Economy.Append(first);
            session.Economy.Append(second);

            Assert.AreEqual(2, session.Economy.Records.Count);
            Assert.AreEqual("purchase:1", session.Economy.Records[0].TransactionIdentity);
            Assert.AreEqual("reroll:1", session.Economy.Records[1].TransactionIdentity);
        }

        [Test]
        public void Parameters_AreTheCatalogsRegister()
        {
            SessionState session = ProgressionFixtures.Session();

            Assert.AreEqual(4, session.Economy.Parameters.StartingRAM);
            Assert.AreEqual(3, session.Economy.Parameters.DeleteRepositoryItemPrice);
        }
    }
}
