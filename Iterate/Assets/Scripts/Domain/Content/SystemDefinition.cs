using System;
using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A frozen System definition: the ordered stages a Session walks. Session Flow walks this list
    /// rather than a hard-coded sequence, so a second System is authored content. Not a content item.
    /// </summary>
    /// <param name="ID">The System's surrogate-key identity.</param>
    /// <param name="DisplayName">The display name shown to the player.</param>
    /// <param name="Stages">The System's stages in walk order.</param>
    public sealed record SystemDefinition(
        SystemID ID,
        string DisplayName,
        IReadOnlyList<SystemStage> Stages
    )
    {
        /// <summary>
        /// The System's stages in walk order. Validated present and non-empty at construction.
        /// </summary>
        public IReadOnlyList<SystemStage> Stages { get; } = RequireStages(Stages);

        /// <summary>
        /// Validates that the System declares at least one stage.
        /// </summary>
        /// <param name="stages">The candidate stage list.</param>
        /// <returns>The list unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the list is absent or empty.</exception>
        private static IReadOnlyList<SystemStage> RequireStages(IReadOnlyList<SystemStage> stages)
        {
            if (stages == null || stages.Count == 0)
                throw new ArgumentException("A SystemDefinition requires at least one stage.", nameof(stages));

            return stages;
        }
    }
}