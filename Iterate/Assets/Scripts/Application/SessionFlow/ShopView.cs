using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The open shop. Every list is a copy.
    /// </summary>
    /// <param name="ShopIdentity">The shop.</param>
    /// <param name="Target">The Process the shop precedes.</param>
    /// <param name="RerollsUsed">How many rerolls were bought.</param>
    /// <param name="NextRerollCost">The next reroll's cost, or null when no reroll is available.</param>
    /// <param name="ServicesEnabled">Whether the item Services are offered.</param>
    /// <param name="Slots">The slots, in order.</param>
    public sealed record ShopView(
        string ShopIdentity,
        ProcessID Target,
        int RerollsUsed,
        int? NextRerollCost,
        bool ServicesEnabled,
        IReadOnlyList<ShopSlotView> Slots
    );
}