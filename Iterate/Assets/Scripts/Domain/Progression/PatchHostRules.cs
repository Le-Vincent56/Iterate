using System;
using System.Collections.Generic;
using Iterate.Domain.Content;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Evaluates a Patch's authored host-eligibility rule against a Repository entry. The rule set is
    /// closed and small, so this is a switch rather than a registry: a fourth rule is a canon revision
    /// that should not compile until it is handled here, and the coverage test over
    /// <see cref="CatalogVocabulary.PatchHostEligibilityRules"/> is what makes that true.
    /// </summary>
    public static class PatchHostRules
    {
        /// <summary>
        /// Whether a Patch may socket to a Repository entry. Only Instructions host Patches, so a
        /// Structure or Directive is ineligible under every rule.
        /// </summary>
        /// <param name="patch">The Patch definition being attached.</param>
        /// <param name="host">The candidate host entry.</param>
        /// <returns>True when the host satisfies the Patch's authored rule.</returns>
        /// <exception cref="ArgumentException">Thrown when the Patch declares an unregistered rule.</exception>
        public static bool IsEligible(PatchDefinition patch, RepositoryEntry host)
        {
            if (patch == null)
                throw new ArgumentException("Eligibility requires a Patch definition.", nameof(patch));

            if (host == null)
                throw new ArgumentException("Eligibility requires a host entry.", nameof(host));

            string rule = patch.HostEligibility?.Rule;
            if (!CatalogVocabulary.PatchHostEligibilityRules.Contains(rule))
            {
                throw new ArgumentException(
                    "The Patch '" + patch.ID.Value + "' declares the unregistered host-eligibility rule '" + rule + "'.",
                    nameof(patch));
            }

            if (host.Item.Kind != RepositoryItemKind.Instruction)
                return false;

            InstructionDefinition definition = host.Item.Instruction.Definition;
            if (NamesPatch(definition.IneligibilityTags, patch.DisplayName))
                return false;

            QuantityChangeOperation primary = definition.PrimaryOperation;
            switch (rule)
            {
                case "FIXED_NUMBER_ADDITION_HOSTS":
                    return primary.Operator == QuantityOperator.Add
                        && primary.Operand.Source == OperandSource.Constant
                        && (primary.Register == CoreRegister.Value || primary.Register == CoreRegister.Signal);

                case "ORDINARY_INSTRUCTION_HOSTS":
                    return true;

                case "SCORE_INSTRUCTION_HOSTS":
                    return primary.Register == CoreRegister.Score;

                default:
                    throw new ArgumentException(
                        "The host-eligibility rule '" + rule + "' is registered but not evaluated.",
                        nameof(patch));
            }
        }

        /// <summary>
        /// Whether an Instruction's ineligibility tags name a modifier by display name.
        /// </summary>
        /// <param name="tags">The Instruction's ineligibility tags.</param>
        /// <param name="displayName">The modifier's display name.</param>
        /// <returns>True when the tags name it.</returns>
        private static bool NamesPatch(IReadOnlyList<string> tags, string displayName)
        {
            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i], displayName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}