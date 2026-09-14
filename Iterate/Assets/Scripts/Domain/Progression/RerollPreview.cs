using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// What the next reroll would cost and replace. The unseen stock is shown because a reroll is
    /// refused when the pool cannot fill every open slot with content this shop has not shown.
    /// </summary>
    /// <param name="Rejection">Why it would be refused; None when it would succeed.</param>
    /// <param name="Cost">The Tokens the next reroll costs.</param>
    /// <param name="OpenSlots">How many slots would be replaced.</param>
    /// <param name="UnseenStock">How many pool members this shop has not shown.</param>
    /// <param name="TokensAfter">The balance after, when it would succeed.</param>
    public sealed record RerollPreview(
        ShopRejection Rejection,
        int Cost,
        int OpenSlots,
        int UnseenStock,
        TokenAmount TokensAfter
    );
}