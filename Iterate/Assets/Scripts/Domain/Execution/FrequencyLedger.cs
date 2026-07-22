using System;
using System.Collections.Generic;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The per-execution frequency ledger: tracks which first-qualifying effect allowances have been
    /// consumed, keyed by <see cref="ActiveEffect.FrequencyKey"/>. Consumption happens at commitment
    /// only — ineligibility, failed qualification, and pre-commitment rejection consume nothing —
    /// and clearing at execution expiration restores every allowance. Every-qualifying effects are
    /// always eligible and never marked.
    /// </summary>
    public sealed class FrequencyLedger
    {
        /// <summary>
        /// The first-qualifying-event allowance token.
        /// </summary>
        private const string FirstQualifyingEvent = "FIRST_QUALIFYING_EVENT";

        /// <summary>
        /// The consumed first-qualifying frequency keys for this execution.
        /// </summary>
        private readonly HashSet<string> _consumed = new HashSet<string>();

        /// <summary>
        /// Reports whether the effect's allowance permits another commitment: false exactly when the
        /// allowance is first-qualifying and its key has been consumed.
        /// </summary>
        /// <param name="effect">The effect to test.</param>
        /// <returns>True when the effect may still commit.</returns>
        /// <exception cref="ArgumentException">Thrown when the effect is null.</exception>
        public bool IsEligible(ActiveEffect effect)
        {
            if (effect == null)
                throw new ArgumentException("Eligibility requires an effect.", nameof(effect));

            if (effect.Frequency.Allowance != FirstQualifyingEvent)
                return true;

            return !_consumed.Contains(effect.FrequencyKey);
        }

        /// <summary>
        /// Marks a first-qualifying effect's allowance consumed. Called at commitment only. Consuming
        /// an every-qualifying effect is a no-op.
        /// </summary>
        /// <param name="effect">The committing effect.</param>
        /// <exception cref="ArgumentException">Thrown when the effect is null.</exception>
        public void Consume(ActiveEffect effect)
        {
            if (effect == null)
                throw new ArgumentException("Consumption requires an effect.", nameof(effect));

            if (effect.Frequency.Allowance != FirstQualifyingEvent)
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
    }
}