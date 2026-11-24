using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Commits the Session to one of the offered routes.
    /// </summary>
    /// <param name="Route">The route to take.</param>
    public sealed record SelectRoute(RouteID Route) : ISessionCommand;
}