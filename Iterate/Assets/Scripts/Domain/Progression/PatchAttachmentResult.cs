using Iterate.Domain.Content;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// The outcome of an attachment. A refusal carries no record, because a refused transaction did
    /// not happen.
    /// </summary>
    /// <param name="Succeeded">Whether the Patch was socketed.</param>
    /// <param name="Rejection">Why it was refused; None on success.</param>
    /// <param name="Record">The committed transaction; null on refusal.</param>
    /// <param name="Attached">The Patch instance minted; null on refusal.</param>
    public sealed record PatchAttachmentResult(
        bool Succeeded,
        PatchAttachmentRejection Rejection,
        TransactionRecord Record,
        PatchInstance Attached
    );
}