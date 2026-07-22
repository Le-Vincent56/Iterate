using System;

namespace Iterate.Domain.Execution
{
    /// <summary>
    /// One near-miss at a boundary: an effect that matched family, subtype, and band but failed a
    /// qualifier. The failed requirement is the failing qualifier rendered as its kind and value.
    /// </summary>
    /// <param name="Effect">The effect that failed to qualify.</param>
    /// <param name="FailedRequirement">The failed requirement, rendered kind:value; non-empty.</param>
    public sealed record EffectNearMiss(ActiveEffect Effect, string FailedRequirement)
    {
        /// <summary>
        /// The effect that failed to qualify. Validated non-null at construction.
        /// </summary>
        public ActiveEffect Effect { get; } = RequireEffect(Effect);

        /// <summary>
        /// The failed requirement, rendered kind:value. Validated non-empty at construction.
        /// </summary>
        public string FailedRequirement { get; } = RequireRequirement(FailedRequirement);

        /// <summary>
        /// Validates that the effect is present.
        /// </summary>
        /// <param name="effect">The candidate effect.</param>
        /// <returns>The effect unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the effect is null.</exception>
        private static ActiveEffect RequireEffect(ActiveEffect effect)
        {
            if (effect == null)
                throw new ArgumentException("A near-miss requires an effect.", nameof(effect));

            return effect;
        }

        /// <summary>
        /// Validates that the failed requirement is non-empty.
        /// </summary>
        /// <param name="failedRequirement">The candidate requirement.</param>
        /// <returns>The requirement unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the requirement is null or empty.</exception>
        private static string RequireRequirement(string failedRequirement)
        {
            if (string.IsNullOrEmpty(failedRequirement))
                throw new ArgumentException("A near-miss requires a failed requirement.", nameof(failedRequirement));

            return failedRequirement;
        }
    }
}