using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// A Patch grant created by a transaction.
    /// </summary>
    /// <param name="Grant">The grant's identity.</param>
    /// <param name="DefinitionID">The Patch definition's identity.</param>
    /// <param name="Cached">Whether the grant may be held indefinitely.</param>
    public sealed record PatchGranted(InstanceID Grant, string DefinitionID, bool Cached) : TransactionConsequence;
}