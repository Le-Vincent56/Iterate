namespace Iterate.Domain.Progression
{
    /// <summary>
    /// Why a Patch attachment was refused. Checked in this order, so the reason a caller sees is the
    /// first thing actually wrong rather than whichever check ran last.
    /// </summary>
    public enum PatchAttachmentRejection
    {
        None,
        UnknownHost,
        HostNotAnInstruction,
        HostIneligible,
        SocketOutOfRange,
        UnknownGrant,
        GrantDefinitionMismatch,
        ServicesNotOffered,
        InsufficientTokens
    }
}