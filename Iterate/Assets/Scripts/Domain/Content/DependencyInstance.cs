using System;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A runtime Dependency instance: a frozen <see cref="DependencyDefinition"/> paired with a stable
    /// <see cref="InstanceID"/>. Identity, not the definition, distinguishes two installed copies of the
    /// same Dependency.
    /// </summary>
    /// <param name="InstanceID">The stable instance identity.</param>
    /// <param name="Definition">The frozen definition this instance realises.</param>
    public sealed record DependencyInstance(
        InstanceID InstanceID,
        DependencyDefinition Definition
    )
    {
        /// <summary>
        /// The frozen definition this instance realises. Validated non-null at construction.
        /// </summary>
        public DependencyDefinition Definition { get; } = RequireDefinition(Definition);

        /// <summary>
        /// Validates that the paired definition is present.
        /// </summary>
        /// <param name="definition">The candidate definition.</param>
        /// <returns>The definition unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the definition is null.</exception>
        private static DependencyDefinition RequireDefinition(DependencyDefinition definition)
        {
            if (definition == null)
                throw new ArgumentException("A DependencyInstance requires a definition.", nameof(definition));

            return definition;
        }
    }
}