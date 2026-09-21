using Iterate.Domain.Progression;
using Iterate.Domain.Values;

namespace Iterate.Application.Persistence
{
    /// <summary>
    /// One installed Dependency as a checkpoint holds it. The price paid is carried because a
    /// destruction refunds against it rather than against the current catalog price.
    /// </summary>
    /// <param name="Instance">The Dependency instance's identity.</param>
    /// <param name="Definition">The Dependency definition's stable identity.</param>
    /// <param name="PricePaid">The Tokens paid to install it.</param>
    /// <param name="Origin">How the Dependency was obtained.</param>
    public sealed record CheckpointDependency(
        InstanceID Instance,
        string Definition,
        int PricePaid,
        DependencyOrigin Origin
    );
}