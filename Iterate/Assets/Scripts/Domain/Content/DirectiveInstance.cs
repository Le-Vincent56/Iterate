using System;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A runtime Directive instance: a frozen <see cref="DirectiveDefinition"/> paired with a stable
    /// <see cref="InstanceID"/>. Once activated as a pragma the same instance carries its identity into
    /// the compilation, so two activated copies of one Directive ledger independently.
    /// </summary>
    /// <param name="InstanceID">The stable instance identity.</param>
    /// <param name="Definition">The frozen definition this instance realises.</param>
    public sealed record DirectiveInstance(InstanceID InstanceID, DirectiveDefinition Definition)
    {
        /// <summary>
        /// The frozen definition this instance realises. Validated non-null at construction.
        /// </summary>
        public DirectiveDefinition Definition { get; } = RequireDefinition(Definition);

        /// <summary>
        /// Validates that the paired definition is present.
        /// </summary>
        /// <param name="definition">The candidate definition.</param>
        /// <returns>The definition unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the definition is null.</exception>
        private static DirectiveDefinition RequireDefinition(DirectiveDefinition definition)
        {
            if (definition == null)
                throw new ArgumentException("A DirectiveInstance requires a definition.", nameof(definition));

            return definition;
        }
    }
}