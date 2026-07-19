using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A frozen Instruction definition: a source object whose primary operation is a quantity change
    /// on a register. Source activation is its trigger, so it carries no trigger descriptor. The
    /// position qualifier is null except for the final-line Instruction; the ineligibility tags are
    /// empty except for the line-number Instruction.
    /// </summary>
    /// <param name="ID">The Instruction's surrogate-key identity.</param>
    /// <param name="RulesText">The canonical rules text.</param>
    /// <param name="DisplayName">The display name shown to the player.</param>
    /// <param name="Category">The content category.</param>
    /// <param name="Rarity">The rarity tier.</param>
    /// <param name="Tags">The definition's tag set.</param>
    /// <param name="SourceFootprint">The number of source positions the Instruction occupies.</param>
    /// <param name="PrimaryOperation">The quantity-change operation the Instruction performs.</param>
    /// <param name="PositionQualifier">The positional qualifier, or null when unconditional.</param>
    /// <param name="IneligibilityTags">Definition names this Instruction is ineligible for, or empty.</param>
    public sealed record InstructionDefinition(
        InstructionID ID,
        string RulesText,
        string DisplayName,
        ContentCategory Category,
        Rarity Rarity,
        IReadOnlyList<string> Tags,
        int SourceFootprint,
        QuantityChangeOperation PrimaryOperation,
        TriggerQualifier PositionQualifier,
        IReadOnlyList<string> IneligibilityTags
    ) : ContentDefinition(RulesText, DisplayName, Category, Rarity, Tags);
}