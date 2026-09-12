namespace Iterate.Domain.Content
{
    /// <summary>
    /// One fixed shop offer: the offer's own stable identity, the content offered, and the price in
    /// Tokens. The price belongs to the offer, never to the content definition, so the same content
    /// may be offered at different prices in different shops.
    /// </summary>
    /// <param name="OfferID">The offer's stable identity.</param>
    /// <param name="Content">The content ID offered.</param>
    /// <param name="Price">The price in Tokens.</param>
    public sealed record ShopOffer(string OfferID, string Content, int Price);
}