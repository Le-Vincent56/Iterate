using System;
using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Resolves one reward component by choosing one of its options, first destroying the named
    /// Dependencies when the choice needs RAM.
    /// </summary>
    /// <param name="ComponentIndex">The reward component being resolved.</param>
    /// <param name="ContentID">The content identity of the chosen option.</param>
    /// <param name="DestroyFirst">The installed Dependencies to destroy before granting; empty for none.</param>
    public sealed record SelectReward(int ComponentIndex, string ContentID, IReadOnlyList<InstanceID> DestroyFirst) : ISessionCommand
    {
        /// <summary>
        /// The installed Dependencies to destroy before granting; empty for none. Validated non-null
        /// at construction, so "destroy nothing" has one spelling.
        /// </summary>
        public IReadOnlyList<InstanceID> DestroyFirst { get; } = RequireDestroyFirst(DestroyFirst);

        /// <summary>
        /// Validates the destroy-first list.
        /// </summary>
        /// <param name="destroyFirst">The candidate list.</param>
        /// <returns>The list unchanged.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the list is null.</exception>
        private static IReadOnlyList<InstanceID> RequireDestroyFirst(IReadOnlyList<InstanceID> destroyFirst)
        {
            if (destroyFirst == null)
                throw new ArgumentNullException(nameof(destroyFirst), "A SelectReward requires a list; pass an empty one to destroy nothing.");

            return destroyFirst;
        }
    }
}