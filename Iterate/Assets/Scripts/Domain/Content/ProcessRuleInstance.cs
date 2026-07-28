using System;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A Process rule as configured on one Process: the frozen definition plus the instance identity
    /// its effects carry as their origin.
    /// </summary>
    /// <param name="InstanceID">The instance identity.</param>
    /// <param name="Definition">The frozen definition this instance realizes.</param>
    public sealed record ProcessRuleInstance(InstanceID InstanceID, ProcessRuleDefinition Definition)
    {
        /// <summary>
        /// The frozen definition this instance realizes. Validated non-null at construction.
        /// </summary>
        public ProcessRuleDefinition Definition { get; } = RequireDefinition(Definition);

        /// <summary>
        /// Validates that the definition is present.
        /// </summary>
        /// <param name="definition">The candidate definition.</param>
        /// <returns>The definition unchanged.</returns>
        /// <exception cref="ArgumentException">Thrown when the definition is null.</exception>
        private static ProcessRuleDefinition RequireDefinition(ProcessRuleDefinition definition)
        {
            if (definition == null)
                throw new ArgumentException("A ProcessRuleInstance requires a definition.", nameof(definition));

            return definition;
        }
    }
}