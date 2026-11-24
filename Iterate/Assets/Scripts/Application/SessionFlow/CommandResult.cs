using System;

namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The outcome of one command: whether it was accepted, why not if refused, the Domain's own
    /// result record, and the stage the Session stands in afterwards. Only <see cref="Accept"/> and
    /// <see cref="Reject"/> build one, so an accepted result carrying a rejection cannot exist.
    /// Equality compares the Domain result by reference; compare members, not results.
    /// </summary>
    public sealed record CommandResult
    {
        /// <summary>
        /// Whether the command was accepted.
        /// </summary>
        public bool Accepted { get; }

        /// <summary>
        /// Why the command was refused; None when accepted.
        /// </summary>
        public CommandRejection Rejection { get; }

        /// <summary>
        /// The Domain's result record exactly as the Domain returned it, its typed rejection when the
        /// Domain refused, or null when no Domain operation ran.
        /// </summary>
        public object DomainResult { get; }

        /// <summary>
        /// The stage the Session stands in after the command.
        /// </summary>
        public SessionStage Stage { get; }

        /// <summary>
        /// Creates a result; reached only through the two factories.
        /// </summary>
        /// <param name="accepted">Whether the command was accepted.</param>
        /// <param name="rejection">Why the command was refused.</param>
        /// <param name="domainResult">The Domain's result record.</param>
        /// <param name="stage">The stage after the command.</param>
        private CommandResult(
            bool accepted,
            CommandRejection rejection,
            object domainResult,
            SessionStage stage
        )
        {
            Accepted = accepted;
            Rejection = rejection;
            DomainResult = domainResult;
            Stage = stage;
        }

        /// <summary>
        /// Creates an accepted result.
        /// </summary>
        /// <param name="domainResult">The Domain's result record, or null when no Domain operation ran.</param>
        /// <param name="stage">The stage after the command.</param>
        /// <returns>The accepted result.</returns>
        public static CommandResult Accept(object domainResult, SessionStage stage)
        {
            return new CommandResult(true, CommandRejection.None, domainResult, stage);
        }

        /// <summary>
        /// Creates a refused result.
        /// </summary>
        /// <param name="rejection">Why the command was refused; never None.</param>
        /// <param name="domainResult">The Domain's typed rejection, or null when no Domain operation ran.</param>
        /// <param name="stage">The stage the Session still stands in.</param>
        /// <returns>The refused result.</returns>
        /// <exception cref="ArgumentException">Thrown when the rejection is None.</exception>
        public static CommandResult Reject(CommandRejection rejection, object domainResult, SessionStage stage)
        {
            if (rejection == CommandRejection.None)
                throw new ArgumentException("A refused CommandResult requires a rejection other than None.", nameof(rejection));

            return new CommandResult(false, rejection, domainResult, stage);
        }
    }
}