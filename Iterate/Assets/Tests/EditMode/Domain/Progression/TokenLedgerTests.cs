using NUnit.Framework;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests the Session's Token ledger: a running balance that starts where the Session starts it,
    /// refuses a debit it cannot cover without writing an entry, and keeps every accepted movement with
    /// the basis it happened under, so a Session's Token history reads without replaying it. The refusal
    /// discipline is what makes an economy transaction's validate-then-commit possible at all.
    /// </summary>
    public sealed class TokenLedgerTests
    {
        [Test]
        public void Balance_NewLedger_StartsAtItsOpeningAmount()
        {
            TokenLedger ledger = new(new TokenAmount(0));

            Assert.AreEqual(new TokenAmount(0), ledger.Balance);
            Assert.AreEqual(0, ledger.Entries.Count);
        }

        [Test]
        public void Credit_RaisesTheBalanceAndWritesAnEntry()
        {
            TokenLedger ledger = new(new TokenAmount(0));

            TokenLedgerResult result = ledger.Credit(new TokenAmount(4), TokenBasis.RewardTokens, "WB-RWD-001:0");

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(new TokenAmount(4), ledger.Balance);
            Assert.AreEqual(1, ledger.Entries.Count);
            Assert.IsFalse(ledger.Entries[0].IsDebit);
            Assert.AreEqual(TokenBasis.RewardTokens, ledger.Entries[0].Basis);
            Assert.AreEqual("WB-RWD-001:0", ledger.Entries[0].Reference);
            Assert.AreEqual(new TokenAmount(4), ledger.Entries[0].Balance);
        }

        [Test]
        public void Debit_WithinBalance_LowersItAndWritesADebitEntry()
        {
            TokenLedger ledger = new(new TokenAmount(4));

            TokenLedgerResult result = ledger.Debit(new TokenAmount(4), TokenBasis.OfferPrice, "OFF-001");

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(new TokenAmount(0), ledger.Balance);
            Assert.IsTrue(ledger.Entries[0].IsDebit);
            Assert.AreEqual(TokenBasis.OfferPrice, ledger.Entries[0].Basis);
        }

        [Test]
        public void Debit_BeyondBalance_IsRefusedAndWritesNothing()
        {
            TokenLedger ledger = new(new TokenAmount(2));

            TokenLedgerResult result = ledger.Debit(new TokenAmount(3), TokenBasis.OfferPrice, "OFF-001");

            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.Entry);
            Assert.AreEqual(new TokenAmount(2), ledger.Balance);
            Assert.AreEqual(0, ledger.Entries.Count);
        }

        [Test]
        public void CanAfford_AnswersAtTheBoundary()
        {
            TokenLedger ledger = new(new TokenAmount(3));

            Assert.IsTrue(ledger.CanAfford(new TokenAmount(3)));
            Assert.IsFalse(ledger.CanAfford(new TokenAmount(4)));
        }

        [Test]
        public void Entries_AreOrderedAndCarryTheirRunningBalance()
        {
            TokenLedger ledger = new(new TokenAmount(0));

            ledger.Credit(new TokenAmount(4), TokenBasis.RewardTokens, "reward");
            ledger.Credit(new TokenAmount(2), TokenBasis.BenchmarkBonus, "benchmark");
            ledger.Debit(new TokenAmount(5), TokenBasis.OfferPrice, "OFF-003");

            Assert.AreEqual(3, ledger.Entries.Count);
            Assert.AreEqual(new TokenAmount(4), ledger.Entries[0].Balance);
            Assert.AreEqual(new TokenAmount(6), ledger.Entries[1].Balance);
            Assert.AreEqual(new TokenAmount(1), ledger.Entries[2].Balance);
            Assert.AreEqual(new TokenAmount(1), ledger.Balance);
        }

        [Test]
        public void Debit_ExactBalance_IsAllowed()
        {
            TokenLedger ledger = new(new TokenAmount(3));

            Assert.IsTrue(ledger.Debit(new TokenAmount(3), TokenBasis.ServicePrice, "delete").Succeeded);
            Assert.AreEqual(new TokenAmount(0), ledger.Balance);
        }

        [Test]
        public void Debit_RefusedAfterAcceptedMovements_LeavesTheHistoryIntact()
        {
            TokenLedger ledger = new(new TokenAmount(0));
            ledger.Credit(new TokenAmount(2), TokenBasis.RewardTokens, "reward");

            ledger.Debit(new TokenAmount(9), TokenBasis.RerollCost, "reroll:1");

            Assert.AreEqual(1, ledger.Entries.Count);
            Assert.AreEqual(new TokenAmount(2), ledger.Balance);
        }

        [Test]
        public void Basis_IsCarriedPerEntryRatherThanPerLedger()
        {
            TokenLedger ledger = new(new TokenAmount(10));

            ledger.Debit(new TokenAmount(4), TokenBasis.OfferPrice, "OFF-001");
            ledger.Debit(new TokenAmount(3), TokenBasis.ReplacementFee, "WB-PAT-001");
            ledger.Credit(new TokenAmount(2), TokenBasis.DestructionRefund, "#12");

            Assert.AreEqual(TokenBasis.OfferPrice, ledger.Entries[0].Basis);
            Assert.AreEqual(TokenBasis.ReplacementFee, ledger.Entries[1].Basis);
            Assert.AreEqual(TokenBasis.DestructionRefund, ledger.Entries[2].Basis);
        }
    }
}
