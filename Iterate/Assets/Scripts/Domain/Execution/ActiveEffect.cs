using System.Globalization;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One registered, interpretable EXECUTION effect: the owning Dependency instance, the definition
    /// identity and effect index, the interpreted trigger, the constant-operand quantity-change
    /// operation, the frequency, and whether the effect transforms the pending operation rather than
    /// reacting to a result.
    /// </summary>
    /// <param name="Origin">The owning Dependency instance's identity.</param>
    /// <param name="DefinitionID">The owning definition's surrogate-key identity.</param>
    /// <param name="EffectIndex">The effect's index within the definition's effect list.</param>
    /// <param name="Trigger">The interpreted trigger descriptor.</param>
    /// <param name="Operation">The constant-operand quantity-change operation.</param>
    /// <param name="Frequency">The effect's frequency declaration.</param>
    /// <param name="IsModification">Whether the effect acts at the modification band.</param>
    public sealed record ActiveEffect(
        InstanceID Origin,
        string DefinitionID,
        int EffectIndex,
        TriggerDescriptor Trigger,
        QuantityChangeOperation Operation,
        EffectFrequency Frequency,
        bool IsModification
    )
    {
        /// <summary>
        /// The per-source-instance frequency-ledger key, composed once at construction as
        /// definition identity, effect index, and owning instance value.
        /// </summary>
        public string FrequencyKey { get; } = DefinitionID + ":" + EffectIndex.ToString(CultureInfo.InvariantCulture) + "#" + Origin.Value.ToString(CultureInfo.InvariantCulture);
    }
}