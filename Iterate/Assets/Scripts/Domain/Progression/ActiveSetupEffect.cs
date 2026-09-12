using System;
using Iterate.Domain.Content;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One process-setup effect active for a Process's resolution, attributed to the content that
    /// declared it. Mirrors Compilation's <c>ActiveCompilationEffect</c>: the engine's own active-effect
    /// type carries execution operations only, so each phase domain that resolves outside execution
    /// brings its own wrapper rather than widening that one.
    /// </summary>
    /// <param name="SourceKey">The stable key identifying the declaring effect.</param>
    /// <param name="SourceDisplayName">The declaring content's display name.</param>
    /// <param name="Effect">The declared process-setup effect.</param>
    public sealed record ActiveSetupEffect(string SourceKey, string SourceDisplayName, EffectDefinition Effect)
    {
        /// <summary>
        /// The declared effect. Validated at construction as a PROCESS_SETUP-domain configuration
        /// modification, so a resolver never has to re-check what it was handed.
        /// </summary>
        public EffectDefinition Effect { get; } = RequireSetupEffect(Effect);

        /// <summary>
        /// The configuration modification this effect applies.
        /// </summary>
        public ConfigurationModificationOperation Modification => (ConfigurationModificationOperation)Effect.Operation;

        /// <summary>
        /// Validates that the effect belongs to the process-setup domain and modifies a configuration.
        /// </summary>
        /// <param name="effect">The candidate effect.</param>
        /// <returns>The effect unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the domain or operation is wrong.</exception>
        private static EffectDefinition RequireSetupEffect(EffectDefinition effect)
        {
            if (effect == null)
                throw new ArgumentException("An active setup effect requires an effect definition.", nameof(effect));

            if (effect.PhaseDomain != PhaseDomain.ProcessSetup)
                throw new ArgumentException("An active setup effect must be declared in the PROCESS_SETUP domain.", nameof(effect));

            if (effect.Operation is not ConfigurationModificationOperation)
                throw new ArgumentException("An active setup effect must carry a configuration modification.", nameof(effect));

            return effect;
        }
    }
}