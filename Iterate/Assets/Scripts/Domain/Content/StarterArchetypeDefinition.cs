using System;
using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A frozen Starter Archetype: the Repository a Session begins with, in acquisition order, and
    /// the starting Dependency installed with it. The starting Repository lists content IDs and
    /// admits duplicates, because two instances of the same definition are two distinct Repository
    /// items. Not a content item.
    /// </summary>
    /// <param name="ID">The archetype's surrogate-key identity.</param>
    /// <param name="DisplayName">The display name shown to the player.</param>
    /// <param name="StartingRepository">The starting content IDs in acquisition order; duplicates admitted.</param>
    /// <param name="StarterDependency">The Dependency installed at Session start.</param>
    public sealed record StarterArchetypeDefinition(
        StarterArchetypeID ID,
        string DisplayName,
        IReadOnlyList<string> StartingRepository,
        DependencyID StarterDependency
    )
    {
        /// <summary>
        /// The starting content IDs in acquisition order. Validated present and non-empty at
        /// construction.
        /// </summary>
        public IReadOnlyList<string> StartingRepository { get; } = RequireStartingRepository(StartingRepository);

        /// <summary>
        /// Validates that the archetype seeds at least one Repository item.
        /// </summary>
        /// <param name="startingRepository">The candidate starting content list.</param>
        /// <returns>The list unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the list is absent or empty.</exception>
        private static IReadOnlyList<string> RequireStartingRepository(IReadOnlyList<string> startingRepository)
        {
            if (startingRepository == null || startingRepository.Count == 0)
                throw new ArgumentException("A StarterArchetypeDefinition requires a starting Repository.", nameof(startingRepository));

            return startingRepository;
        }
    }
}