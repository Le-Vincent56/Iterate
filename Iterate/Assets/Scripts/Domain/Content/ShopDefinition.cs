using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A frozen shop definition: the visible slot count, the four capability switches, the fixed
    /// offers, and the pool a reroll draws from. Reroll costs are parameters, not shop fields. Not a
    /// content item — a shop carries no rules text, category, rarity or tags.
    /// </summary>
    /// <param name="ID">The shop's surrogate-key identity.</param>
    /// <param name="DisplayName">The display name shown to the player.</param>
    /// <param name="Slots">The number of visible offer slots.</param>
    /// <param name="RerollsEnabled">Whether the shop admits rerolls.</param>
    /// <param name="PinningEnabled">Whether the shop admits pinning.</param>
    /// <param name="DependenciesEnabled">Whether the shop offers Dependencies.</param>
    /// <param name="ServicesEnabled">Whether the shop offers Services.</param>
    /// <param name="FixedOffers">The authored fixed offers, in authored order.</param>
    /// <param name="RerollPool">The pool a reroll draws from; null when rerolls are disabled.</param>
    public sealed record ShopDefinition(
        ShopID ID,
        string DisplayName,
        int Slots,
        bool RerollsEnabled,
        bool PinningEnabled,
        bool DependenciesEnabled,
        bool ServicesEnabled,
        IReadOnlyList<ShopOffer> FixedOffers,
        PoolID? RerollPool
    );
}