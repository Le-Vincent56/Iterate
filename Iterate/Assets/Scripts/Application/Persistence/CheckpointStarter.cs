using Iterate.Domain.Values;

namespace Iterate.Application.Persistence
{
    /// <summary>
    /// The Dependency installed at Session start, as a checkpoint holds it.
    /// </summary>
    /// <param name="Instance">The Dependency instance's identity.</param>
    /// <param name="Definition">The Dependency definition's stable identity.</param>
    public sealed record CheckpointStarter(InstanceID Instance, string Definition);
}