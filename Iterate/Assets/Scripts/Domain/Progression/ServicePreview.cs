using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// What a Service would cost and destroy, computed by the same validation the commit runs. The
    /// destroyed-Patch count is shown because deletion destroys attachments with no refund, which is
    /// the part of the price a player cannot see in Tokens.
    /// </summary>
    /// <param name="Rejection">Why it would be refused; None when it would succeed.</param>
    /// <param name="Price">The Tokens the Service costs.</param>
    /// <param name="PatchesDestroyed">How many attached Patches would be destroyed.</param>
    /// <param name="TokensAfter">The balance after, when it would succeed.</param>
    public sealed record ServicePreview(
        ServiceRejection Rejection,
        int Price,
        int PatchesDestroyed,
        TokenAmount TokensAfter
    );
}