using Iterate.Domain.Values;

namespace Iterate.Domain.Content
{
    /// <summary>
    /// A frozen route definition: one selectable branch of a route-selection stage, pairing the shop
    /// that opens before it with the Process it runs. Not a content item.
    /// </summary>
    /// <param name="ID">The route's surrogate-key identity.</param>
    /// <param name="DisplayName">The display name shown to the player.</param>
    /// <param name="Process">The Process configuration this route runs.</param>
    /// <param name="Shop">The shop opening before this route's Process.</param>
    public sealed record RouteDefinition(
        RouteID ID,
        string DisplayName,
        ProcessID Process,
        ShopID Shop
    );
}