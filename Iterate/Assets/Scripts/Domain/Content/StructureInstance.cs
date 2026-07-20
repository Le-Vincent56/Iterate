using System;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A runtime Structure instance: a frozen <see cref="StructureDefinition"/> paired with a stable
    /// <see cref="InstanceID"/>. Containment lives solely in the source arrangement — contained slots
    /// reference their owning instance — so this record carries no child list.
    /// </summary>
    /// <param name="InstanceID">The stable instance identity.</param>
    /// <param name="Definition">The frozen definition this instance realises.</param>
    public sealed record StructureInstance(InstanceID InstanceID, StructureDefinition Definition)
    {
        /// <summary>
        /// The frozen definition this instance realises. Validated non-null at construction.
        /// </summary>
        public StructureDefinition Definition { get; } = RequireDefinition(Definition);

        /// <summary>
        /// Validates that the paired definition is present.
        /// </summary>
        /// <param name="definition">The candidate definition.</param>
        /// <returns>The definition unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the definition is null.</exception>
        private static StructureDefinition RequireDefinition(StructureDefinition definition)
        {
            if (definition == null)
                throw new ArgumentException("A StructureInstance requires a definition.", nameof(definition));

            return definition;
        }
    }
}