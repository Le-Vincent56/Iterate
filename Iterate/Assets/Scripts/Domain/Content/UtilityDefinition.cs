using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A frozen Utility definition: shop content whose process-setup effects modify a Process's
    /// starting configuration.
    /// </summary>
    /// <param name="ID">The Utility's surrogate-key identity.</param>
    /// <param name="RulesText">The canonical rules text.</param>
    /// <param name="DisplayName">The display name shown to the player.</param>
    /// <param name="Category">The content category.</param>
    /// <param name="Rarity">The rarity tier.</param>
    /// <param name="Tags">The definition's tag set.</param>
    /// <param name="Effects">The declarative process-setup effects the Utility resolves.</param>
    public sealed record UtilityDefinition(
        UtilityID ID,
        string RulesText,
        string DisplayName,
        ContentCategory Category,
        Rarity Rarity,
        IReadOnlyList<string> Tags,
        IReadOnlyList<EffectDefinition> Effects
    ) : ContentDefinition(RulesText, DisplayName, Category, Rarity, Tags);
}