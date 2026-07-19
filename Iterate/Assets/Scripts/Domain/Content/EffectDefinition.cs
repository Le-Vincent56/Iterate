namespace Iterate.Domain.Content
{
    /// <summary>
    /// A declarative, phase-domained effect: a trigger descriptor, an operation from the
    /// closed set, a targeting rule, a timing band or boundary, a stacking mode, and a
    /// frequency/scope. Trigger, targeting, timing, and frequency may be null where the phase domain's
    /// moment makes them implicit; the validator enforces per-domain requirements while this record
    /// stays permissive.
    /// </summary>
    /// <param name="PhaseDomain">The phase domain that owns the effect's moment.</param>
    /// <param name="Trigger">The trigger descriptor, or null where the moment is implicit.</param>
    /// <param name="Operation">The operation the effect resolves.</param>
    /// <param name="Targeting">The targeting rule, or null where no target is required.</param>
    /// <param name="Timing">The timing band or named boundary, or null where implicit.</param>
    /// <param name="Stacking">The stacking mode for coexisting instances.</param>
    /// <param name="Frequency">The frequency and reset scope, or null where implicit.</param>
    public sealed record EffectDefinition(
        PhaseDomain PhaseDomain,
        TriggerDescriptor Trigger,
        EffectOperation Operation,
        TargetingRule Targeting,
        EffectTiming Timing,
        StackingMode Stacking,
        EffectFrequency Frequency
    );
}