using System.Collections.Generic;
using Iterate.Domain.Progression;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Copies the open shop into a <see cref="ShopView"/>.
    /// </summary>
    internal static class ShopViewMapper
    {
        /// <summary>
        /// Maps the context's shop.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The view, or null when no shop is open.</returns>
        internal static ShopView Map(SessionContext context)
        {
            ShopState shop = context.Shop;
            if (shop == null)
                return null;

            IReadOnlyList<ShopSlot> slots = shop.Slots;
            List<ShopSlotView> slotViews = new(slots.Count);
            for (int index = 0; index < slots.Count; index++)
            {
                ShopSlot slot = slots[index];
                slotViews.Add(new ShopSlotView(slot.Number, slot.Offer.ContentID, slot.Offer.Category, slot.Offer.Price, slot.State, slot.Pinned));
            }

            return new ShopView(
                shop.Definition.ID.Value,
                shop.TargetProcess,
                shop.RerollsUsed,
                shop.NextRerollCost,
                shop.Definition.ServicesEnabled,
                slotViews
            );
        }
    }
}