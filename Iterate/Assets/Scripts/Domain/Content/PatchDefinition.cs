using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A frozen Patch definition: content socketed to a host, whose host-anchored effects resolve
    /// against the host's activity.
    /// </summary>
    /// <param name="ID">The Patch's surrogate-key identity.</param>
    /// <param name="RulesText">The canonical rules text.</param>
    /// <param name="DisplayName">The display name shown to the player.</param>
    /// <param name="Category">The content category.</param>
    /// <param name="Rarity">The rarity tier.</param>
    /// <param name="Tags">The definition's tag set.</param>
    /// <param name="HostEligibility">The rule naming which host classes the Patch may socket to.</param>
    /// <param name="Effects">The declarative host-anchored effects the Patch resolves.</param>
    public sealed record PatchDefinition(
        PatchID ID,
        string RulesText,
        string DisplayName,
        ContentCategory Category,
        Rarity Rarity,
        IReadOnlyList<string> Tags,
        PatchHostEligibility HostEligibility,
        IReadOnlyList<EffectDefinition> Effects
    ) : ContentDefinition(RulesText, DisplayName, Category, Rarity, Tags);
}