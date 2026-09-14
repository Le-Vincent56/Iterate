using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// What buying a slot would cost, and how it would route — an item to the Repository, a Utility to
    /// a commitment.
    /// </summary>
    /// <param name="Rejection">Why it would be refused; None when it would succeed.</param>
    /// <param name="Price">The Tokens it costs.</param>
    /// <param name="TokensAfter">The balance after, when it would succeed.</param>
    /// <param name="Category">The content category, which decides the routing.</param>
    public sealed record PurchasePreview(
        ShopRejection Rejection,
        int Price,
        TokenAmount TokensAfter,
        ContentCategory Category
    );
}