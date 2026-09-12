namespace Iterate.Domain.Content
{
    /// <summary>
    /// One acquisition-pool membership: the content and, for a purchase pool, its price in Tokens. A
    /// reward pool's members carry no price. The price belongs to the membership, never to the
    /// content definition.
    /// </summary>
    /// <param name="Content">The content ID in the pool.</param>
    /// <param name="Price">The price in Tokens; null in a reward pool.</param>
    public sealed record PoolMember(string Content, int? Price);
}