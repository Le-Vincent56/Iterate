using System;
using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A frozen Structure definition. Enforces the Repeat/Condition field-pairing shape at
    /// construction (defense-in-depth behind the validator): a Repeat carries a positive repeat count
    /// and no predicate; a Condition carries a predicate and a zero repeat count.
    /// </summary>
    /// <param name="ID">The Structure's surrogate-key identity.</param>
    /// <param name="RulesText">The canonical rules text.</param>
    /// <param name="DisplayName">The display name shown to the player.</param>
    /// <param name="Category">The content category.</param>
    /// <param name="Rarity">The rarity tier.</param>
    /// <param name="Tags">The definition's tag set.</param>
    /// <param name="SourceFootprint">The number of source positions the Structure reserves.</param>
    /// <param name="StructureKind">Whether the Structure is a Repeat or a Condition.</param>
    /// <param name="RepeatCount">The iteration count for a Repeat; zero for a Condition.</param>
    /// <param name="Predicate">The Condition predicate; null for a Repeat.</param>
    public sealed record StructureDefinition(
        StructureID ID,
        string RulesText,
        string DisplayName,
        ContentCategory Category,
        Rarity Rarity,
        IReadOnlyList<string> Tags,
        int SourceFootprint,
        StructureKind StructureKind,
        int RepeatCount,
        StructurePredicate Predicate
    ) : ContentDefinition(RulesText, DisplayName, Category, Rarity, Tags)
    {
        /// <summary>
        /// The iteration count for a Repeat; zero for a Condition. Validated against the Structure
        /// kind at construction.
        /// </summary>
        public int RepeatCount { get; } = RequirePairing(StructureKind, RepeatCount, Predicate);

        /// <summary>
        /// Validates the Repeat/Condition field pairing and returns the repeat count unchanged.
        /// </summary>
        /// <param name="structureKind">The Structure kind under construction.</param>
        /// <param name="repeatCount">The candidate repeat count.</param>
        /// <param name="predicate">The candidate predicate.</param>
        /// <returns>The validated repeat count.</returns>
        /// <exception cref="ArgumentException">Thrown when the fields do not match the kind's shape.</exception>
        private static int RequirePairing(StructureKind structureKind, int repeatCount, StructurePredicate predicate)
        {
            if (structureKind == StructureKind.Repeat)
            {
                if (predicate != null)
                    throw new ArgumentException("A Repeat Structure must not carry a predicate.", nameof(predicate));

                if (repeatCount < 1)
                    throw new ArgumentException("A Repeat Structure requires a repeat count of at least one.", nameof(repeatCount));

                return repeatCount;
            }

            if (predicate == null)
                throw new ArgumentException("A Condition Structure requires a predicate.", nameof(predicate));

            if (repeatCount != 0)
                throw new ArgumentException("A Condition Structure must carry a repeat count of zero.", nameof(repeatCount));

            return repeatCount;
        }
    }
}