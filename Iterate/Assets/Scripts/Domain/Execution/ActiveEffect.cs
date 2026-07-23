using System;
using System.Globalization;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One registered, interpretable EXECUTION effect as a validated tagged union: the owning
    /// Dependency instance, the definition identity and effect index, the interpreted trigger, the
    /// participation kind, and exactly one operation — a constant-operand quantity change for the
    /// modification and reaction kinds, a rescue operation for the rescue kind.
    /// </summary>
    /// <param name="Origin">The owning Dependency instance's identity.</param>
    /// <param name="DefinitionID">The owning definition's surrogate-key identity.</param>
    /// <param name="EffectIndex">The effect's index within the definition's effect list.</param>
    /// <param name="Trigger">The interpreted trigger descriptor.</param>
    /// <param name="Kind">How the effect participates at the engine's boundaries.</param>
    /// <param name="Operation">The quantity-change operation; null for a rescue effect.</param>
    /// <param name="Rescue">The rescue operation; null for the other kinds.</param>
    /// <param name="Frequency">The effect's frequency declaration.</param>
    public sealed record ActiveEffect(
        InstanceID Origin,
        string DefinitionID,
        int EffectIndex,
        TriggerDescriptor Trigger,
        ActiveEffectKind Kind,
        QuantityChangeOperation Operation,
        RescueOperation Rescue,
        EffectFrequency Frequency
    )
    {
        /// <summary>
        /// The quantity-change operation; null for a rescue effect. Validated against the kind at
        /// construction: exactly one of the operation pair is present.
        /// </summary>
        public QuantityChangeOperation Operation { get; } = RequirePairing(Kind, Operation, Rescue);

        /// <summary>
        /// Whether the effect transforms the pending operation at the modification band. Derived
        /// from the kind.
        /// </summary>
        public bool IsModification { get; } = Kind == ActiveEffectKind.Modification;

        /// <summary>
        /// The per-source-instance frequency-ledger key, composed once at construction as
        /// definition identity, effect index, and owning instance value.
        /// </summary>
        public string FrequencyKey { get; } = DefinitionID + ":" + EffectIndex.ToString(CultureInfo.InvariantCulture) + "#" + Origin.Value.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Validates the kind/operation pairing and returns the quantity-change operation unchanged.
        /// </summary>
        /// <param name="kind">The participation kind under construction.</param>
        /// <param name="operation">The candidate quantity-change operation.</param>
        /// <param name="rescue">The candidate rescue operation.</param>
        /// <returns>The validated quantity-change operation; null for the rescue kind.</returns>
        /// <exception cref="ArgumentException">Thrown when the operation pair does not match the kind.</exception>
        private static QuantityChangeOperation RequirePairing(
            ActiveEffectKind kind,
            QuantityChangeOperation operation,
            RescueOperation rescue
        )
        {
            if (kind == ActiveEffectKind.Rescue)
            {
                if (rescue == null)
                    throw new ArgumentException("A rescue-kind ActiveEffect requires a rescue operation.", nameof(rescue));

                if (operation != null)
                    throw new ArgumentException("A rescue-kind ActiveEffect must not carry a quantity change.", nameof(operation));

                return null;
            }

            if (operation == null)
                throw new ArgumentException("A modification- or reaction-kind ActiveEffect requires a quantity change.", nameof(operation));

            if (rescue != null)
                throw new ArgumentException("A modification- or reaction-kind ActiveEffect must not carry a rescue operation.", nameof(rescue));

            return operation;
        }
    }
}