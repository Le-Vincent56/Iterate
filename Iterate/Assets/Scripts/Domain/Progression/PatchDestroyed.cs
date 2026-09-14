using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// A Patch destroyed by a transaction — replaced in its socket, or lost with its host. A destroyed
    /// Patch instance identity never recurs.
    /// </summary>
    /// <param name="Host">The host it was socketed to.</param>
    /// <param name="Socket">The socket it occupied.</param>
    /// <param name="Patch">The destroyed Patch instance.</param>
    /// <param name="DefinitionID">The Patch definition's identity.</param>
    public sealed record PatchDestroyed(
        InstanceID Host,
        int Socket,
        InstanceID Patch,
        string DefinitionID
    ) : TransactionConsequence;
}