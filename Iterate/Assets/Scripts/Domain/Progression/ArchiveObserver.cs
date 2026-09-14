using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One installed Dependency's standing interest in archives, interpreted once when a Process
    /// opens. It carries its own origin so a credited Byte is attributable, and its frequency so the
    /// Process knows whether it may fire again.
    /// </summary>
    /// <param name="Origin">The Dependency instance that declared it.</param>
    /// <param name="DefinitionID">The Dependency definition's identity.</param>
    /// <param name="DisplayName">The Dependency's display name, used as the ledger reason.</param>
    /// <param name="Gain">What firing it gains.</param>
    /// <param name="Frequency">How often it may fire.</param>
    public sealed record ArchiveObserver(
        InstanceID Origin,
        string DefinitionID,
        string DisplayName,
        ResourceGainOperation Gain,
        EffectFrequency Frequency
    );
}