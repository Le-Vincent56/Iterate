using Iterate.Domain.Content;
using Iterate.Domain.Progression;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// One shop slot and its offer.
    /// </summary>
    /// <param name="Number">The one-based slot number.</param>
    /// <param name="ContentID">The offered content.</param>
    /// <param name="Category">The offered content's category.</param>
    /// <param name="Price">The price in Tokens.</param>
    /// <param name="State">Whether the slot is available, sold or exhausted.</param>
    /// <param name="Pinned">Whether the slot is pinned.</param>
    public sealed record ShopSlotView(
        int Number,
        string ContentID,
        ContentCategory Category,
        int Price,
        ShopSlotState State,
        bool Pinned
    );
}