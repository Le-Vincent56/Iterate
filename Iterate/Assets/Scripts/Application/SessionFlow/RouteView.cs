using System.Collections.Generic;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The route choice.
    /// </summary>
    /// <param name="Options">The routes offered, in authored order.</param>
    /// <param name="Committed">The route committed to, or null.</param>
    public sealed record RouteView(IReadOnlyList<RouteOptionView> Options, RouteID? Committed);
}