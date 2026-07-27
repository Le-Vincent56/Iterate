using System;
using System.Collections.Generic;
using System.Globalization;
using Iterate.Domain.Trace;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// The per-execution frequency ledger: tracks which limited effect allowances have been
    /// consumed, keyed by <see cref="ActiveEffect.FrequencyKey"/> and, for a source-execution-scoped
    /// allowance, additionally by the triggering unit's identity — so one attachment may commit once
    /// under each unit it observes rather than once per execution. Consumption happens at commitment
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
        /// The reset-scope token whose allowance is per source execution of the observed unit.
        /// </summary>
        private const string SourceExecutionScope = "SOURCE_EXECUTION";

        /// <summary>
        /// The consumed limited-allowance frequency keys for this execution.
        /// </summary>
        private readonly HashSet<string> _consumed = new HashSet<string>();

        /// <summary>
        /// Reports whether the effect's allowance permits another commitment, with no triggering
        /// unit in scope.
        /// </summary>
        /// <param name="effect">The effect to test.</param>
        /// <returns>True when the effect may still commit.</returns>
        /// <exception cref="ArgumentException">Thrown when the effect is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a source-execution-scoped limited effect is offered with no unit.</exception>
        public bool IsEligible(ActiveEffect effect)
        {
            return IsEligible(effect, null);
        }

        /// <summary>
        /// Reports whether the effect's allowance permits another commitment against the triggering
        /// unit: false exactly when the allowance is limited and its resolved key has been consumed.
        /// </summary>
        /// <param name="effect">The effect to test.</param>
        /// <param name="triggeringUnit">The unit whose event is being offered, or null when source-less.</param>
        /// <returns>True when the effect may still commit.</returns>
        /// <exception cref="ArgumentException">Thrown when the effect is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a source-execution-scoped limited effect is offered with no unit.</exception>
        public bool IsEligible(ActiveEffect effect, RuntimeUnitID? triggeringUnit)
        {
            if (effect == null)
                throw new ArgumentException("Eligibility requires an effect.", nameof(effect));

            if (!IsLimited(effect))
                return true;

            return !_consumed.Contains(ResolveKey(effect, triggeringUnit));
        }

        /// <summary>
        /// Marks a limited effect's allowance consumed, with no triggering unit in scope.
        /// </summary>
        /// <param name="effect">The committing effect.</param>
        /// <exception cref="ArgumentException">Thrown when the effect is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a source-execution-scoped limited effect is offered with no unit.</exception>
        public void Consume(ActiveEffect effect)
        {
            Consume(effect, null);
        }

        /// <summary>
        /// Marks a limited effect's allowance consumed against the triggering unit. Called at
        /// commitment only. Consuming an every-qualifying effect is a no-op.
        /// </summary>
        /// <param name="effect">The committing effect.</param>
        /// <param name="triggeringUnit">The unit whose event caused the commitment, or null when source-less.</param>
        /// <exception cref="ArgumentException">Thrown when the effect is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a source-execution-scoped limited effect is offered with no unit.</exception>
        public void Consume(ActiveEffect effect, RuntimeUnitID? triggeringUnit)
        {
            if (effect == null)
                throw new ArgumentException("Consumption requires an effect.", nameof(effect));

            if (!IsLimited(effect))
                return;

            _consumed.Add(ResolveKey(effect, triggeringUnit));
        }

        /// <summary>
        /// Drops every consumed key — the execution-expiration reset.
        /// </summary>
        public void Clear()
        {
            _consumed.Clear();
        }

        /// <summary>
        /// Resolves the ledger key a limited effect consumes under: the bare frequency key for every
        /// scope but the source-execution scope, which appends the triggering unit's ordinal.
        /// </summary>
        /// <param name="effect">The limited effect.</param>
        /// <param name="triggeringUnit">The unit whose event is being offered, or null when source-less.</param>
        /// <returns>The ledger key.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a source-execution-scoped effect is offered with no unit.</exception>
        private static string ResolveKey(ActiveEffect effect, RuntimeUnitID? triggeringUnit)
        {
            if (effect.Frequency.Scope != SourceExecutionScope)
                return effect.FrequencyKey;

            if (!triggeringUnit.HasValue)
                throw new InvalidOperationException($"The source-execution-scoped effect '{effect.FrequencyKey}' was offered with no triggering unit.");

            return effect.FrequencyKey + "@u" + triggeringUnit.Value.Value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reports whether the effect declares a limited allowance the ledger tracks.
        /// </summary>
        /// <param name="effect">The effect to classify.</param>
        /// <returns>True when the allowance is limited.</returns>
        private static bool IsLimited(ActiveEffect effect)
        {
            string allowance = effect.Frequency.Allowance;
            return allowance is FirstQualifyingEvent or Once;
        }
    }
}