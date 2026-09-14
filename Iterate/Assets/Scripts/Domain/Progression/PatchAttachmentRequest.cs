using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One attachment a caller is asking for: which Patch, onto which host, in which socket, at what
    /// price. A grant identity means the Patch was awarded rather than bought, and the price is then
    /// zero — the request carries both so a rejected grant cannot silently become a free purchase.
    /// </summary>
    /// <param name="Patch">The Patch definition to socket.</param>
    /// <param name="Host">The host instance identity.</param>
    /// <param name="Socket">The socket to occupy.</param>
    /// <param name="Price">The Tokens the Patch costs.</param>
    /// <param name="Grant">The grant being consumed, or null for a purchase.</param>
    /// <param name="Reference">The offer or reward component this refers to.</param>
    public sealed record PatchAttachmentRequest(
        PatchDefinition Patch,
        InstanceID Host,
        int Socket,
        int Price,
        InstanceID? Grant,
        string Reference
    );
}