using System;
using System.Collections.Generic;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The routes keyed by (command type, stage). A command is legal exactly when a route exists for
    /// it in the current stage, and dispatch invokes that route, so there is no separate legality
    /// check to drift from the handlers and no switch over command types. Registering a key twice is
    /// a wiring defect and throws.
    /// </summary>
    public sealed class CommandRegistry
    {
        private readonly Dictionary<CommandRouteKey, CommandRoute> _routes = new();

        /// <summary>
        /// How many routes are registered.
        /// </summary>
        public int Count => _routes.Count;

        /// <summary>
        /// Registers a route.
        /// </summary>
        /// <param name="route">The route to register.</param>
        /// <exception cref="ArgumentNullException">Thrown when the route is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a route for the same key is already registered.</exception>
        public void Add(CommandRoute route)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));

            if (!_routes.TryAdd(route.Key, route))
                throw new ArgumentException($"A route for {route.Key.Command?.Name} in {route.Key.Stage} is already registered.", nameof(route));
        }

        /// <summary>
        /// Whether a command type is legal in a stage.
        /// </summary>
        /// <param name="command">The command type.</param>
        /// <param name="stage">The stage.</param>
        /// <returns>True when a route exists for the pair.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the command type is null.</exception>
        public bool IsLegal(Type command, SessionStage stage)
        {
            return TryGet(command, stage, out CommandRoute _);
        }

        /// <summary>
        /// Finds the route for a command type in a stage.
        /// </summary>
        /// <param name="command">The command type.</param>
        /// <param name="stage">The stage.</param>
        /// <param name="route">The route when found; null otherwise.</param>
        /// <returns>True when a route exists for the pair.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the command type is null.</exception>
        public bool TryGet(Type command, SessionStage stage, out CommandRoute route)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            return _routes.TryGetValue(new CommandRouteKey(command, stage), out route);
        }
    }
}