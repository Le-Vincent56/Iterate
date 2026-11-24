using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Attaches a Patch to a host Instruction's socket: in a shop, by buying the Patch in a slot; at
    /// rewards, by placing a granted Patch. Exactly one of the slot and the grant applies, chosen by
    /// the stage the command is raised in.
    /// </summary>
    /// <param name="Slot">The shop slot holding the Patch; null at rewards.</param>
    /// <param name="Grant">The granted Patch to place; null in a shop.</param>
    /// <param name="Host">The Instruction that receives the Patch.</param>
    /// <param name="Socket">The host socket to attach to.</param>
    public sealed record InstallPatch(
        int? Slot,
        InstanceID? Grant,
        InstanceID Host,
        int Socket
    ) : ISessionCommand;
}