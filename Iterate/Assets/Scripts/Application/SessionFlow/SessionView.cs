using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The Session as a whole: where it stands, its economy, and what it owns. Every list is a copy.
    /// </summary>
    /// <param name="Stage">The stage.</param>
    /// <param name="Tokens">The Token balance.</param>
    /// <param name="RAMCapacity">The RAM available to installations.</param>
    /// <param name="RAMUsage">The RAM installations hold.</param>
    /// <param name="Repository">The owned items, in acquisition order.</param>
    /// <param name="Dependencies">The installed Dependencies, starter first.</param>
    /// <param name="CommittedRoute">The route committed to, or null.</param>
    public sealed record SessionView(
        SessionStage Stage,
        int Tokens,
        int RAMCapacity,
        int RAMUsage,
        IReadOnlyList<RepositoryItemView> Repository,
        IReadOnlyList<DependencyView> Dependencies,
        RouteID? CommittedRoute
    );
}