using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// What an attachment would do, computed by the same validation the commit runs — so a preview
    /// cannot drift from the transaction it previews.
    /// </summary>
    /// <param name="Rejection">Why it would be refused; None when it would succeed.</param>
    /// <param name="IsReplacement">Whether the socket is already occupied.</param>
    /// <param name="Replaced">The Patch instance that would be destroyed; null otherwise.</param>
    /// <param name="Price">The Patch's price.</param>
    /// <param name="Fee">The replacement fee; zero when the socket is empty.</param>
    /// <param name="TokensBefore">The balance before.</param>
    /// <param name="TokensAfter">The balance after, when it would succeed.</param>
    public sealed record PatchAttachmentPreview(
        PatchAttachmentRejection Rejection,
        bool IsReplacement,
        PatchInstance Replaced,
        int Price,
        int Fee,
        TokenAmount TokensBefore,
        TokenAmount TokensAfter
    );
}