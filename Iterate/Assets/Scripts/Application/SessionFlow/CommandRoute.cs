using System;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// A legal (command type, stage) pair and the handler that applies the command there. A route
    /// existing is what makes the command legal, so legality and dispatch cannot disagree.
    /// </summary>
    /// <param name="Key">The command type and stage this route serves.</param>
    /// <param name="Apply">The handler applying the command to the Session context.</param>
    public sealed record CommandRoute(CommandRouteKey Key, Func<SessionContext, ISessionCommand, HandlerOutcome> Apply)
    {
        /// <summary>
        /// The handler applying the command to the Session context. Validated non-null at
        /// construction.
        /// </summary>
        public Func<SessionContext, ISessionCommand, HandlerOutcome> Apply { get; } = RequireApply(Apply);

        /// <summary>
        /// Validates the handler.
        /// </summary>
        /// <param name="apply">The candidate handler.</param>
        /// <returns>The handler unchanged.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the handler is null.</exception>
        private static Func<SessionContext, ISessionCommand, HandlerOutcome> RequireApply(
            Func<SessionContext, ISessionCommand, HandlerOutcome> apply
        )
        {
            if (apply == null)
                throw new ArgumentNullException(nameof(apply), "A CommandRoute requires a handler.");

            return apply;
        }
    }
}