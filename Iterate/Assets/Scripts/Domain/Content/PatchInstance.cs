using System;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A runtime Patch instance: a frozen <see cref="PatchDefinition"/> paired with a stable
    /// <see cref="InstanceID"/>. Attachment is recorded on the host instance, so a Patch's identity is
    /// what makes two otherwise-identical hosts mechanically distinct (UX-SRC-016).
    /// </summary>
    /// <param name="InstanceID">The stable instance identity.</param>
    /// <param name="Definition">The frozen definition this instance realises.</param>
    public sealed record PatchInstance(InstanceID InstanceID, PatchDefinition Definition)
    {
        /// <summary>
        /// The frozen definition this instance realises. Validated non-null at construction.
        /// </summary>
        public PatchDefinition Definition { get; } = RequireDefinition(Definition);

        /// <summary>
        /// Validates that the paired definition is present.
        /// </summary>
        /// <param name="definition">The candidate definition.</param>
        /// <returns>The definition unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the definition is null.</exception>
        private static PatchDefinition RequireDefinition(PatchDefinition definition)
        {
            if (definition == null)
                throw new ArgumentException("A PatchInstance requires a definition.", nameof(definition));

            return definition;
        }
    }
}