using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A frozen Process-rule definition: a rule owned by a Process rather than by any player-held
    /// item. Never drafted, purchased, rerolled, or destroyed, it reaches the runtime through the
    /// Process's configuration rather than a Branch, Buffer, or socket. Its rarity is carried only
    /// because every content row declares one; it has no behavioral meaning here.
    /// </summary>
    /// <param name="ID">The Process rule's surrogate-key identity.</param>
    /// <param name="RulesText">The canonical rules text.</param>
    /// <param name="DisplayName">The display name shown to the player.</param>
    /// <param name="Category">The content category.</param>
    /// <param name="Rarity">The rarity tier; not meaningful for a Process rule.</param>
    /// <param name="Tags">The definition's tag set.</param>
    /// <param name="Effects">The declarative effects the Process rule resolves.</param>
    public sealed record ProcessRuleDefinition(
        ProcessRuleID ID,
        string RulesText,
        string DisplayName,
        ContentCategory Category,
        Rarity Rarity,
        IReadOnlyList<string> Tags,
        IReadOnlyList<EffectDefinition> Effects
    ) : ContentDefinition(RulesText, DisplayName, Category, Rarity, Tags);
}