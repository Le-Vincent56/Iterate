using System.Collections.Generic;
using Iterate.Domain.Content;
using Iterate.Domain.Values;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// Copies the route choice into a <see cref="RouteView"/>.
    /// </summary>
    internal static class RouteViewMapper
    {
        /// <summary>
        /// Maps the context's route choice.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The view, or null outside the route choice.</returns>
        internal static RouteView Map(SessionContext context)
        {
            if (context.Stage != SessionStage.RouteSelection)
                return null;

            IReadOnlyList<RouteID> routes = context.Plan.Current.Routes;
            List<RouteOptionView> options = new(routes.Count);
            for (int index = 0; index < routes.Count; index++)
            {
                if (context.Catalog.TryGetRoute(routes[index], out RouteDefinition route))
                    options.Add(new RouteOptionView(route.ID, route.DisplayName, route.Process, route.Shop));
            }

            return new RouteView(options, context.Plan.CommittedRoute);
        }
    }
}