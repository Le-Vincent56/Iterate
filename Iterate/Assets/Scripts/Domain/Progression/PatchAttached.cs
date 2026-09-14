using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// A Patch socketed to a host by a transaction.
    /// </summary>
    /// <param name="Host">The host Instruction instance.</param>
    /// <param name="Socket">The socket it occupies.</param>
    /// <param name="Patch">The Patch instance minted.</param>
    /// <param name="DefinitionID">The Patch definition's identity.</param>
    public sealed record PatchAttached(
        InstanceID Host,
        int Socket,
        InstanceID Patch,
        string DefinitionID
    ) : TransactionConsequence;
}