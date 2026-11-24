using System;
using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Buys and installs the Dependency in a shop slot, first destroying the named Dependencies to
    /// free RAM.
    /// </summary>
    /// <param name="Slot">The shop slot holding the Dependency.</param>
    /// <param name="DestroyFirst">The installed Dependencies to destroy before installing; empty for none.</param>
    public sealed record InstallDependency(int Slot, IReadOnlyList<InstanceID> DestroyFirst) : ISessionCommand
    {
        /// <summary>
        /// The installed Dependencies to destroy before installing; empty for none. Validated
        /// non-null at construction, so "destroy nothing" has one spelling.
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
                throw new ArgumentNullException(nameof(destroyFirst), "An InstallDependency requires a list; pass an empty one to destroy nothing.");

            return destroyFirst;
        }
    }
}