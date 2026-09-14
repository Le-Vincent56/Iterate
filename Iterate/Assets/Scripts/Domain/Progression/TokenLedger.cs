using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The Session's Tokens. A debit beyond the balance is refused and changes nothing, so no
    /// transaction can half-spend and every economy operation can validate before it commits. Tokens
    /// are Session-scoped and carry across Processes, unlike Bytes.
    /// </summary>
    public sealed class TokenLedger
    {
        private readonly List<TokenLedgerEntry> _entries = new();

        /// <summary>
        /// The Tokens currently available.
        /// </summary>
        public TokenAmount Balance { get; private set; }

        /// <summary>
        /// Every accepted movement, in order.
        /// </summary>
        public IReadOnlyList<TokenLedgerEntry> Entries => _entries;

        public TokenLedger(TokenAmount startingTokens)
        {
            Balance = startingTokens;
        }

        /// <summary>
        /// Whether the balance covers an amount.
        /// </summary>
        /// <param name="amount">The Tokens to test against.</param>
        /// <returns>True when the balance is at least the amount.</returns>
        public bool CanAfford(TokenAmount amount) => amount.Value <= Balance.Value;

        /// <summary>
        /// Spends Tokens, refusing a debit the balance cannot cover. The comparison precedes the
        /// subtraction because <see cref="TokenAmount"/> clamps at zero, so subtracting first would
        /// silently produce a zero balance instead of a refusal.
        /// </summary>
        /// <param name="amount">The Tokens to spend.</param>
        /// <param name="basis">Why the Tokens are being spent.</param>
        /// <param name="reference">What the spend refers to.</param>
        /// <returns>The ledger result.</returns>
        public TokenLedgerResult Debit(TokenAmount amount, TokenBasis basis, string reference)
        {
            if (amount.Value > Balance.Value)
                return new TokenLedgerResult(false, Balance, null);

            Balance = new TokenAmount(Balance.Value - amount.Value);
            TokenLedgerEntry entry = new(amount, true, basis, reference, Balance);
            _entries.Add(entry);
            return new TokenLedgerResult(true, Balance, entry);
        }

        /// <summary>
        /// Grants Tokens.
        /// </summary>
        /// <param name="amount">The Tokens to grant.</param>
        /// <param name="basis">Why the Tokens are being granted.</param>
        /// <param name="reference">What the grant refers to.</param>
        /// <returns>The ledger result.</returns>
        public TokenLedgerResult Credit(TokenAmount amount, TokenBasis basis, string reference)
        {
            Balance = new TokenAmount(Balance.Value + amount.Value);
            TokenLedgerEntry entry = new(amount, false, basis, reference, Balance);
            _entries.Add(entry);
            return new TokenLedgerResult(true, Balance, entry);
        }
    }
}