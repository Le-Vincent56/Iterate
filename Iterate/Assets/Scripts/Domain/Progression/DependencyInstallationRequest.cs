using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Domain.Progression
{
    /// <summary>
    /// One installation a caller is asking for, including any Dependencies to destroy first. The
    /// destructions are part of the request rather than a separate transaction because their refunds
    /// and released RAM are what make the installation affordable and fit (UX-MOD-025).
    /// </summary>
    /// <param name="Dependency">The Dependency definition to install.</param>
    /// <param name="Price">The Tokens it costs; zero for a reward.</param>
    /// <param name="Origin">How it is being acquired.</param>
    /// <param name="DestroyFirst">The installed Dependencies to destroy as part of this transaction.</param>
    /// <param name="Reference">The offer or reward component this refers to.</param>
    public sealed record DependencyInstallationRequest(
        DependencyDefinition Dependency,
        int Price,
        DependencyOrigin Origin,
        IReadOnlyList<InstanceID> DestroyFirst,
        string Reference
    );
}