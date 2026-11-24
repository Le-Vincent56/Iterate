using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// One step of a System as the Session will play it: a Process, a shop, a route choice, or the
    /// end. Exactly the reference the kind needs is present; the route list is empty for every kind
    /// but a route choice.
    /// </summary>
    /// <param name="Kind">What the step asks the Session to do.</param>
    /// <param name="Process">The Process to enter; null for every other kind.</param>
    /// <param name="Shop">The shop to open; null for every other kind.</param>
    /// <param name="Routes">The routes offered; empty for every other kind.</param>
    public sealed record PlannedStep(
        PlannedStepKind Kind,
        ProcessID? Process,
        ShopID? Shop,
        IReadOnlyList<RouteID> Routes
    );
}