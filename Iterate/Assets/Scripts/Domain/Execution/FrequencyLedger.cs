using System;
using System.Collections.Generic;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The per-execution frequency ledger: tracks which limited effect allowances have been
    /// consumed, keyed by <see cref="ActiveEffect.FrequencyKey"/>. Consumption happens at commitment
    /// only — ineligibility, failed qualification, and pre-commitment rejection consume nothing —
    /// and clearing at execution expiration restores every allowance. First-qualifying and
    /// once-per-execution allowances are both limited and consume identically at this ledger's
    /// per-execution lifetime; every-qualifying effects are always eligible and never marked.
    /// </summary>
    public sealed class FrequencyLedger
    {
        /// <summary>
        /// The first-qualifying-event allowance token.
        /// </summary>
        private const string FirstQualifyingEvent = "FIRST_QUALIFYING_EVENT";

        /// <summary>
        /// The once-per-execution allowance token.
        /// </summary>
        private const string Once = "ONCE";

        /// <summary>
        /// The consumed limited-allowance frequency keys for this execution.
        /// </summary>
        private readonly HashSet<string> _consumed = new HashSet<string>();

        /// <summary>
        /// Reports whether the effect's allowance permits another commitment: false exactly when the
        /// allowance is limited and its key has been consumed.
        /// </summary>
        /// <param name="effect">The effect to test.</param>
        /// <returns>True when the effect may still commit.</returns>
        /// <exception cref="ArgumentException">Thrown when the effect is null.</exception>
        public bool IsEligible(ActiveEffect effect)
        {
            if (effect == null)
                throw new ArgumentException("Eligibility requires an effect.", nameof(effect));

            if (!IsLimited(effect))
                return true;

            return !_consumed.Contains(effect.FrequencyKey);
        }

        /// <summary>
        /// Marks a limited effect's allowance consumed. Called at commitment only. Consuming an
        /// every-qualifying effect is a no-op.
        /// </summary>
        /// <param name="effect">The committing effect.</param>
        /// <exception cref="ArgumentException">Thrown when the effect is null.</exception>
        public void Consume(ActiveEffect effect)
        {
            if (effect == null)
                throw new ArgumentException("Consumption requires an effect.", nameof(effect));

            if (!IsLimited(effect))
                return;

            _consumed.Add(effect.FrequencyKey);
        }

        /// <summary>
        /// Drops every consumed key — the execution-expiration reset.
        /// </summary>
        public void Clear()
        {
            _consumed.Clear();
        }

        /// <summary>
        /// Reports whether the effect declares a limited allowance the ledger tracks.
        /// </summary>
        /// <param name="effect">The effect to classify.</param>
        /// <returns>True when the allowance is limited.</returns>
        private static bool IsLimited(ActiveEffect effect)
        {
            string allowance = effect.Frequency.Allowance;
            return allowance == FirstQualifyingEvent || allowance == Once;
        }
    }
}