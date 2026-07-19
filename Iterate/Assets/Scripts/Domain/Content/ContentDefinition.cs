using System.Collections.Generic;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// The immutable common identity core shared by every content definition: rules text, display
    /// name, category, rarity, and tag set.
    /// </summary>
    /// <param name="RulesText">The canonical rules text.</param>
    /// <param name="DisplayName">The display name shown to the player.</param>
    /// <param name="Category">The content category.</param>
    /// <param name="Rarity">The rarity tier.</param>
    /// <param name="Tags">The definition's tag set.</param>
    public abstract record ContentDefinition(
        string RulesText,
        string DisplayName,
        ContentCategory Category,
        Rarity Rarity,
        IReadOnlyList<string> Tags
    );
}