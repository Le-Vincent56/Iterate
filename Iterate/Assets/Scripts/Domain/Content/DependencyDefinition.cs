using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A frozen Dependency definition: an installed global modifier that consumes RAM and resolves one
    /// or more declarative effects.
    /// </summary>
    /// <param name="ID">The Dependency's surrogate-key identity.</param>
    /// <param name="RulesText">The canonical rules text.</param>
    /// <param name="DisplayName">The display name shown to the player.</param>
    /// <param name="Category">The content category.</param>
    /// <param name="Rarity">The rarity tier.</param>
    /// <param name="Tags">The definition's tag set.</param>
    /// <param name="RAM">The RAM the installed Dependency consumes.</param>
    /// <param name="Effects">The declarative effects the Dependency resolves.</param>
    public sealed record DependencyDefinition(
        DependencyID ID,
        string RulesText,
        string DisplayName,
        ContentCategory Category,
        Rarity Rarity,
        IReadOnlyList<string> Tags,
        int RAM,
        IReadOnlyList<EffectDefinition> Effects
    ) : ContentDefinition(RulesText, DisplayName, Category, Rarity, Tags);
}