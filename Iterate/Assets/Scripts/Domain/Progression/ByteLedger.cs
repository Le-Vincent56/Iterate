using System;
using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// A Process's Bytes. The Compilation caller debits it after a committed compilation reports its
    /// cost; a debit beyond the balance is refused and changes nothing, so the balance can never go
    /// negative and a refusal never half-spends. Bytes are Process-scoped and do not carry out.
    /// </summary>
    public sealed class ByteLedger
    {
        private readonly List<LedgerEntry> _entries = new();

        /// <summary>
        /// The Bytes currently available.
        /// </summary>
        public ByteAmount Balance { get; private set; }

        /// <summary>
        /// Every accepted movement, in order.
        /// </summary>
        public IReadOnlyList<LedgerEntry> Entries => _entries;

        public ByteLedger(ByteAmount startingBytes)
        {
            Balance = startingBytes;
        }

        /// <summary>
        /// Spends Bytes, refusing a debit the balance cannot cover.
        /// </summary>
        /// <param name="amount">The Bytes to spend.</param>
        /// <param name="reason">Why the Bytes are being spent.</param>
        /// <returns>The ledger result.</returns>
        public LedgerResult Debit(ByteAmount amount, string reason)
        {
            if (amount.Value > Balance.Value)
                return new LedgerResult(false, Balance, null);

            Balance = new ByteAmount(Balance.Value - amount.Value);
            LedgerEntry entry = new(amount, true, reason, Balance);
            _entries.Add(entry);
            return new LedgerResult(true, Balance, entry);
        }

        /// <summary>
        /// Grants Bytes.
        /// </summary>
        /// <param name="amount">The Bytes to grant.</param>
        /// <param name="reason">Why the Bytes are being granted.</param>
        /// <returns>The ledger result.</returns>
        public LedgerResult Credit(ByteAmount amount, string reason)
        {
            Balance = new ByteAmount(Balance.Value + amount.Value);
            LedgerEntry entry = new(amount, false, reason, Balance);
            _entries.Add(entry);
            return new LedgerResult(true, Balance, entry);
        }
    }
}