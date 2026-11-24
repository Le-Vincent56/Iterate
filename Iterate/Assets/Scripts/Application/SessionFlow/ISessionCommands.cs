namespace Iterate.Application.SessionFlow
{
    /// <summary>
    /// The one way player intent reaches the Session.
    /// </summary>
    public interface ISessionCommands
    {
        /// <summary>
        /// Applies a command if it is legal in the current stage and permitted by every gate.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>The result.</returns>
        CommandResult Execute(ISessionCommand command);
    }
}