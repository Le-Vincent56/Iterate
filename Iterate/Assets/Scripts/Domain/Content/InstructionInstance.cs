using System;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A runtime Instruction instance: a frozen <see cref="InstructionDefinition"/> paired with a stable
    /// <see cref="InstanceID"/> and an optional attached <see cref="PatchInstance"/>. Identity, not the
    /// definition, distinguishes two instances; attaching a Patch produces a derived record under the
    /// same identity.
    /// </summary>
    /// <param name="InstanceID">The stable instance identity.</param>
    /// <param name="Definition">The frozen definition this instance realises.</param>
    /// <param name="AttachedPatch">The socketed Patch instance, or null when unpatched.</param>
    public sealed record InstructionInstance(
        InstanceID InstanceID,
        InstructionDefinition Definition,
        PatchInstance AttachedPatch
    )
    {
        /// <summary>
        /// The frozen definition this instance realises. Validated non-null at construction.
        /// </summary>
        public InstructionDefinition Definition { get; } = RequireDefinition(Definition);

        /// <summary>
        /// Validates that the paired definition is present.
        /// </summary>
        /// <param name="definition">The candidate definition.</param>
        /// <returns>The definition unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the definition is null.</exception>
        private static InstructionDefinition RequireDefinition(InstructionDefinition definition)
        {
            if (definition == null)
                throw new ArgumentException("An InstructionInstance requires a definition.", nameof(definition));

            return definition;
        }
    }
}