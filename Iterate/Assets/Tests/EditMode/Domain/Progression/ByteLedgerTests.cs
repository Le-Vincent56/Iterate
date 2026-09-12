using NUnit.Framework;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression.Tests
{
    /// <summary>
    /// Tests the Process's Byte ledger: a balance the Compilation caller debits, which refuses a debit
    /// it cannot afford and changes nothing when it does. Bytes are Process-scoped and never negative.
    /// </summary>
    public sealed class ByteLedgerTests
    {
        [Test]
        public void ANewLedger_HoldsItsStartingBytes()
        {
            ByteLedger ledger = new(new ByteAmount(3));

            Assert.AreEqual(3, ledger.Balance.Value);
            Assert.AreEqual(0, ledger.Entries.Count);
        }

        [Test]
        public void Debit_WithinTheBalance_Succeeds()
        {
            ByteLedger ledger = new(new ByteAmount(3));

            LedgerResult result = ledger.Debit(new ByteAmount(2), "edited compilation");

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(1, ledger.Balance.Value);
        }

        [Test]
        public void Debit_OfTheWholeBalance_Succeeds()
        {
            ByteLedger ledger = new(new ByteAmount(3));

            Assert.IsTrue(ledger.Debit(new ByteAmount(3), "edited compilation").Succeeded);
            Assert.AreEqual(0, ledger.Balance.Value);
        }

        [Test]
        public void Debit_BeyondTheBalance_IsRejectedAndChangesNothing()
        {
            ByteLedger ledger = new(new ByteAmount(3));

            LedgerResult result = ledger.Debit(new ByteAmount(4), "edited compilation");

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(3, ledger.Balance.Value, "a rejected debit leaves the balance alone.");
            Assert.AreEqual(0, ledger.Entries.Count, "and writes no entry.");
        }

        [Test]
        public void Debit_OfZero_Succeeds()
        {
            ByteLedger ledger = new(new ByteAmount(0));

            Assert.IsTrue(ledger.Debit(new ByteAmount(0), "free compilation").Succeeded);
            Assert.AreEqual(0, ledger.Balance.Value);
        }

        [Test]
        public void Debit_FromAnEmptyLedger_IsRejected()
        {
            ByteLedger ledger = new(new ByteAmount(0));

            Assert.IsFalse(ledger.Debit(new ByteAmount(1), "edited compilation").Succeeded);
        }

        [Test]
        public void Entries_RecordEveryAcceptedDebitInOrder()
        {
            ByteLedger ledger = new(new ByteAmount(6));

            ledger.Debit(new ByteAmount(1), "first edited compilation");
            ledger.Debit(new ByteAmount(2), "second edited compilation");

            Assert.AreEqual(2, ledger.Entries.Count);
            Assert.AreEqual(1, ledger.Entries[0].Amount.Value);
            Assert.AreEqual("first edited compilation", ledger.Entries[0].Reason);
            Assert.AreEqual(2, ledger.Entries[1].Amount.Value);
            Assert.AreEqual(3, ledger.Entries[1].BalanceAfter.Value);
        }

        [Test]
        public void Credit_RaisesTheBalance()
        {
            ByteLedger ledger = new(new ByteAmount(1));

            ledger.Credit(new ByteAmount(2), "BYTE CACHE");

            Assert.AreEqual(3, ledger.Balance.Value);
            Assert.AreEqual(1, ledger.Entries.Count);
        }

        [Test]
        public void ARejectedDebit_LeavesTheLedgerUsable()
        {
            ByteLedger ledger = new(new ByteAmount(2));

            ledger.Debit(new ByteAmount(5), "too expensive");

            Assert.IsTrue(ledger.Debit(new ByteAmount(2), "affordable").Succeeded);
            Assert.AreEqual(0, ledger.Balance.Value);
            Assert.AreEqual(1, ledger.Entries.Count);
        }
    }
}
