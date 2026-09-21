using Iterate.Domain.Values;

namespace Iterate.Application.Persistence
{
    /// <summary>
    /// Where a restored Session resumes: the Process, the route that led to it, and how the player
    /// re-enters.
    /// </summary>
    /// <param name="Process">The Process to resume at.</param>
    /// <param name="Route">The route taken to it, or null.</param>
    /// <param name="Recovery">How the player re-enters the Process.</param>
    public sealed record CheckpointDestination(ProcessID Process, RouteID? Route, CheckpointRecovery Recovery);
}