using System;
using System.Collections.Generic;
using Iterate.Domain.Content;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Interprets a Dependency's BUILD_INTERACTION effects into archive observers. Fail-fast on
    /// purpose: this slice interprets exactly one shape, so any other BUILD_INTERACTION effect throws
    /// naming its definition rather than being skipped. A skipped effect is a Dependency that
    /// under-executes silently, which is the failure class the whole interpreter exists to prevent.
    /// </summary>
    public static class BuildInteractionEffects
    {
        private const string ArchiveSubtype = "OBJECT_ARCHIVED";

        private const string FirstQualifyingEvent = "FIRST_QUALIFYING_EVENT";

        private const string ProcessScope = "PROCESS";

        /// <summary>
        /// Interprets the archive observers an installed Dependency declares.
        /// </summary>
        /// <param name="dependency">The installed Dependency instance.</param>
        /// <returns>Its observers; empty when it declares no build-interaction effects.</returns>
        /// <exception cref="ArgumentException">Thrown when a BUILD_INTERACTION effect is uninterpretable.</exception>
        public static IReadOnlyList<ArchiveObserver> Interpret(DependencyInstance dependency)
        {
            if (dependency == null)
                throw new ArgumentException("Interpretation requires a Dependency instance.", nameof(dependency));

            List<ArchiveObserver> observers = new();
            IReadOnlyList<EffectDefinition> effects = dependency.Definition.Effects;
            for (int i = 0; i < effects.Count; i++)
            {
                EffectDefinition effect = effects[i];
                if (effect.PhaseDomain != PhaseDomain.BuildInteraction)
                    continue;

                observers.Add(Interpret(dependency, effect));
            }

            return observers;
        }

        /// <summary>
        /// Interprets one build-interaction effect, rejecting every shape but the archive gain.
        /// </summary>
        /// <param name="dependency">The declaring Dependency instance.</param>
        /// <param name="effect">The declared effect.</param>
        /// <returns>The observer.</returns>
        /// <exception cref="ArgumentException">Thrown when the effect is not an archive gain.</exception>
        private static ArchiveObserver Interpret(DependencyInstance dependency, EffectDefinition effect)
        {
            string definitionID = dependency.Definition.ID.Value;
            if (effect.Trigger == null)
            {
                throw new ArgumentException(
                    "The Dependency '" + definitionID + "' declares a BUILD_INTERACTION effect with no trigger.",
                    nameof(effect));
            }

            if (effect.Trigger.EventFamily != EventFamily.ContentLifecycle
                || !string.Equals(effect.Trigger.EventSubtype, ArchiveSubtype, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The Dependency '" + definitionID + "' observes '" + effect.Trigger.EventSubtype
                        + "', which no build interaction in this slice interprets.",
                    nameof(effect));
            }

            if (effect.Operation is not ResourceGainOperation gain)
            {
                throw new ArgumentException(
                    "The Dependency '" + definitionID + "' declares a BUILD_INTERACTION effect whose operation is not a resource gain.",
                    nameof(effect));
            }

            if (effect.Frequency == null
                || !string.Equals(effect.Frequency.Allowance, FirstQualifyingEvent, StringComparison.Ordinal)
                || !string.Equals(effect.Frequency.Scope, ProcessScope, StringComparison.Ordinal)
            )
            {
                throw new ArgumentException(
                    "The Dependency '" + definitionID + "' declares a build interaction whose frequency is not FIRST_QUALIFYING_EVENT per PROCESS.",
                    nameof(effect)
                );
            }

            return new ArchiveObserver(
                dependency.InstanceID,
                definitionID,
                dependency.Definition.DisplayName,
                gain,
                effect.Frequency
            );
        }
    }
}